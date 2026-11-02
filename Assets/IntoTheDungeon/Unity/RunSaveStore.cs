using System.IO;
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity
{
    /// <summary>이어하기 저장 파일(persistentDataPath/run_save.json) 읽기·쓰기.</summary>
    public static class RunSaveStore
    {
        static string FilePath => Path.Combine(Application.persistentDataPath, "run_save.json");

        public static bool HasSave
        {
            get { try { return File.Exists(FilePath); } catch { return false; } }
        }

        public static void Write(string json)
        {
            try { File.WriteAllText(FilePath, json); }
            catch (System.Exception e) { Debug.LogWarning($"[저장] 쓰기 실패: {e.Message}"); }
        }

        /// <summary>저장된 판을 되살린다. 없거나 깨졌으면 null(깨진 파일은 지운다).</summary>
        public static RunData Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var run = RunData.FromSave(JsonUtility.FromJson<RunSave>(File.ReadAllText(FilePath)));
                if (run == null) Delete();
                return run;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[저장] 불러오기 실패: {e.Message}");
                Delete();
                return null;
            }
        }

        public static void Delete()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
        }
    }

    /// <summary>판을 넘어 쌓이는 진행(잼 등) 파일(persistentDataPath/meta.json). 게임을 켤 때 한 번 읽는다.</summary>
    public static class MetaStore
    {
        static string FilePath => Path.Combine(Application.persistentDataPath, "meta.json");
        static int s_savedVersion = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    MetaProgress.Set(MetaProgress.FromSave(JsonUtility.FromJson<MetaSave>(File.ReadAllText(FilePath))));
            }
            catch (System.Exception e) { Debug.LogWarning($"[저장] 잼 기록 불러오기 실패: {e.Message}"); }
            s_savedVersion = MetaProgress.Current.Version;
        }

        /// <summary>바뀐 게 있으면 저장.</summary>
        public static void SaveIfChanged()
        {
            var meta = MetaProgress.Current;
            if (meta.Version == s_savedVersion) return;
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(meta.ToSave()));
                s_savedVersion = meta.Version;
            }
            catch (System.Exception e) { Debug.LogWarning($"[저장] 잼 기록 쓰기 실패: {e.Message}"); }
        }
    }

    /// <summary>
    /// 자동 저장: 지도에서 방을 고르는 중이거나 정비 화면이면 0.5초마다 판을 확인해 바뀌었으면 저장한다
    /// (전투 중엔 저장하지 않으므로 끄면 그 방에 들어가기 전으로). 판이 끝나면(클리어·실패) 저장을 지운다.
    /// </summary>
    public sealed class RunAutoSave : MonoBehaviour, IWorldInjectable
    {
        const float Interval = 0.5f;
        string _last;
        float _timer;

        public void Init(IWorld world) { }

        void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = Interval;
            Save();
        }

        void OnApplicationQuit() => Save();

        void Save()
        {
            MetaStore.SaveIfChanged();   // 잼은 전투 중에 얻어도 바로 남긴다
            var run = RunState.Current;
            if (run.Phase is RunPhase.Cleared or RunPhase.Failed)
            {
                if (_last != null || RunSaveStore.HasSave) { RunSaveStore.Delete(); _last = null; }
                return;
            }
            if (!run.CanSave) return;
            var json = JsonUtility.ToJson(run.ToSave());
            if (json == _last) return;
            _last = json;
            RunSaveStore.Write(json);
        }
    }
}

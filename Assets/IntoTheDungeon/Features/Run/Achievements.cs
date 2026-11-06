using System.Collections.Generic;

namespace IntoTheDungeon.Features.Run
{
    /// <summary>업적 하나. 그림은 동료 얼굴(Recipe) 또는 잼 아이콘(Gem) 중 하나.</summary>
    public sealed class AchievementDef
    {
        public string Id, Title, Condition;
        public string Recipe, Gem;
        public System.Func<MetaProgress, bool> Done;
    }

    /// <summary>
    /// 업적 목록(타이틀 화면 「업적」에서 본다). 판을 넘어 쌓인 MetaProgress로 판정한다.
    /// 동료 업적: 동료 방·물음표 방에서 처음 만나면 해금(영입 안 해도). 못 만난 동료는 얼굴·이름을 가린다.
    /// </summary>
    public static class Achievements
    {
        static List<AchievementDef> _all;

        public static IReadOnlyList<AchievementDef> All => _all ??= Build();

        public static int DoneCount(MetaProgress meta)
        {
            int n = 0;
            foreach (var a in All) if (a.Done(meta)) n++;
            return n;
        }

        static List<AchievementDef> Build()
        {
            var list = new List<AchievementDef>();
            foreach (var r in CardPool.Recruits)
            {
                string recipe = r.RecruitRecipe;
                list.Add(new AchievementDef
                {
                    Id = "ally_" + recipe,
                    Title = r.Title.Replace("동료 영입: ", ""),
                    Condition = "만나기",
                    Recipe = recipe,
                    Done = m => m.AllyUnlocked(recipe)
                });
            }
            list.Add(new AchievementDef { Id = "boss1", Title = "첫 보스", Condition = "보스 처치", Gem = Encounters.BossGemOf(1), Done = m => m.BossKills >= 1 });
            list.Add(new AchievementDef { Id = "boss3", Title = "사냥꾼", Condition = "보스 3회 처치", Gem = Encounters.EliteGem, Done = m => m.BossKills >= 3 });
            list.Add(new AchievementDef { Id = "clear1", Title = "던전 정복", Condition = "던전 클리어", Gem = Encounters.BossGemOf(2), Done = m => m.Clears >= 1 });
            return list;
        }
    }
}

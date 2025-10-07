using IntoTheDungeon.Core.ECS.Components;

namespace IntoTheDungeon.Features.Command
{
    public struct CharacterIntentBuffer : IComponentData
    {
        // 고정 크기 배열 (최대 8개)
        private const int MAX_INTENTS = 8;
        
        private CharacterIntent _intent0;
        private CharacterIntent _intent1;
        private CharacterIntent _intent2;
        private CharacterIntent _intent3;
        private CharacterIntent _intent4;
        private CharacterIntent _intent5;
        private CharacterIntent _intent6;
        private CharacterIntent _intent7;
        
        public int Count;
        
        public readonly bool HasIntent => Count > 0;
        
        public void Add(CharacterIntent intent)
        {
            if (Count >= MAX_INTENTS)
            {
                // 버퍼 가득 참 - 오래된 것 제거하거나 무시
                UnityEngine.Debug.LogWarning("Intent buffer full!");
                return;
            }

            // Switch로 적절한 슬롯에 저장
            switch (Count)
            {
                case 0: _intent0 = intent; break;
                case 1: _intent1 = intent; break;
                case 2: _intent2 = intent; break;
                case 3: _intent3 = intent; break;
                case 4: _intent4 = intent; break;
                case 5: _intent5 = intent; break;
                case 6: _intent6 = intent; break;
                case 7: _intent7 = intent; break;
            }

            Count++;
        }
        
        public readonly CharacterIntent Get(int index)
        {
            if (index < 0 || index >= Count)
                throw new System.IndexOutOfRangeException();
            
            return index switch
            {
                0 => _intent0,
                1 => _intent1,
                2 => _intent2,
                3 => _intent3,
                4 => _intent4,
                5 => _intent5,
                6 => _intent6,
                7 => _intent7,
                _ => default
            };
        }
        
        public CharacterIntent Consume()
        {
            if (Count == 0)
                return default;
            
            var intent = _intent0;
            
            // Shift 모든 Intent를 앞으로
            _intent0 = _intent1;
            _intent1 = _intent2;
            _intent2 = _intent3;
            _intent3 = _intent4;
            _intent4 = _intent5;
            _intent5 = _intent6;
            _intent6 = _intent7;
            
            Count--;
            return intent;
        }
        
        public void Clear()
        {
            Count = 0;
        }
    }
}
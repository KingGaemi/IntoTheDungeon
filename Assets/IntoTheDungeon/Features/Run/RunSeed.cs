namespace IntoTheDungeon.Features.Run
{
    /// <summary>
    /// 판 시드 ↔ 6자 코드(헷갈리는 0/O/1/I/L을 뺀 31자 알파벳). 결산·기록에 표시하고 재도전·입력에 쓴다.
    /// 31^6 ≈ 8.9억이라 int 시드 전체를 담진 못한다: Encode는 시드를 31^6으로 접어 넣는다.
    /// 코드로 시작한 판(TryDecode → StartNew(seed:))은 같은 코드가 다시 나온다(코드 → 시드 → 코드 일치).
    /// 저장된 판의 재도전은 코드가 아니라 RunData.Seed 정수를 그대로 쓸 것.
    /// </summary>
    public static class RunSeed
    {
        public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";   // 31자
        public const int Length = 6;
        const long Space = 31L * 31 * 31 * 31 * 31 * 31;   // 887,503,681

        /// <summary>시드 → 6자 코드.</summary>
        public static string Encode(int seed)
        {
            long v = ((long)seed % Space + Space) % Space;   // 음수도 0 이상으로 접는다
            var chars = new char[Length];
            for (int i = Length - 1; i >= 0; i--)
            {
                chars[i] = Alphabet[(int)(v % Alphabet.Length)];
                v /= Alphabet.Length;
            }
            return new string(chars);
        }

        /// <summary>6자 코드 → 시드(1 이상). 소문자·공백은 관용. 모양이 틀리면 false.</summary>
        public static bool TryDecode(string code, out int seed)
        {
            seed = 0;
            if (string.IsNullOrEmpty(code)) return false;
            code = code.Trim().ToUpperInvariant();
            if (code.Length != Length) return false;
            long v = 0;
            foreach (char c in code)
            {
                int d = Alphabet.IndexOf(c);
                if (d < 0) return false;
                v = v * Alphabet.Length + d;
            }
            if (v == 0) v = 1;   // 0은 "시드 없음"이라 피한다
            seed = (int)v;
            return true;
        }
    }
}

using GTA.Math;

namespace LSOL.Domain
{
    public sealed class BankDefinition
    {
        public string BankId { get; set; }

        public string Name { get; set; }

        public Vector3 Position { get; set; }

        public float LoanAmountMaxLimit { get; set; }

        public float LoanInterestMin { get; set; }

        public float LoanInterestMax { get; set; }

        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(Name) ? (BankId ?? string.Empty) : Name; }
        }
    }
}
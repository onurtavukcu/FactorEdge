namespace FactorEdge.Application.Features.Users.Models
{

    public sealed class ProductionEfficiencyDto
    {
        public long ProductionCount { get; set; }
        public long ScrapCount { get; set; }
        public double ScrapRate { get; set; }
        public double RunningRate { get; set; }
        public double ObservedMinutes { get; set; }
    }
}

using System.Text.Json.Serialization;

public class ScripPriceDto
{
    [JsonPropertyName("contractRate")]
    public decimal ContractRate { get; set; }

    [JsonPropertyName("contractQuantity")]
    public decimal? ContractQuantity { get; set; }

    [JsonPropertyName("time")]
    public long Time { get; set; } // Unix timestamp
}
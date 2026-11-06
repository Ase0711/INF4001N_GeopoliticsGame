using System.ComponentModel.DataAnnotations;

namespace GeopoliticsGame.Web.Models;

public sealed class DeployRequest
{
    [Required]
    public Dictionary<string, int> Allocation { get; set; } = new();
}

public sealed class TransferRequest
{
    [Required, RegularExpression("^[A-Za-z]{3}$")]
    public string FromCode { get; set; } = string.Empty;

    [Required, RegularExpression("^[A-Za-z]{3}$")]
    public string ToCode { get; set; } = string.Empty;

    [Range(1, 10_000, ErrorMessage = "Move between 1 and 10,000 soldiers.")]
    public int Amount { get; set; }
}

public sealed class AttackRequest
{
    [Required, RegularExpression("^[A-Za-z]{3}$")]
    public string FromCode { get; set; } = string.Empty;

    [Required, RegularExpression("^[A-Za-z]{3}$")]
    public string ToCode { get; set; } = string.Empty;
}

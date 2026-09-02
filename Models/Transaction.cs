using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace OnlineBankingSystem.Models;

public enum TransactionType
{
    Deposit,
    Withdrawal,
    Transfer
}

public class Transaction
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("accountNumber")]
    public string AccountNumber { get; set; } = string.Empty;

    [BsonElement("relatedAccountNumber")]
    public string? RelatedAccountNumber { get; set; }

    [BsonElement("type")]
    [BsonRepresentation(BsonType.String)]
    public TransactionType Type { get; set; }

    [BsonElement("amount")]
    public decimal Amount { get; set; }

    [BsonElement("balanceAfter")]
    public decimal BalanceAfter { get; set; }

    [BsonElement("date")]
    public DateTime Date { get; set; } = DateTime.UtcNow;

    [BsonElement("note")]
    public string Note { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = "Completed";
}

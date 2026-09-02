using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace OnlineBankingSystem.Models;

public class Account
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("accountNumber")]
    public string AccountNumber { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("phoneNumber")]
    public string PhoneNumber { get; set; } = string.Empty;

    [BsonElement("balance")]
    public decimal Balance { get; set; }

    [BsonElement("accountType")]
    public string AccountType { get; set; } = "Standard";

    [BsonElement("status")]
    public string Status { get; set; } = "Active";

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

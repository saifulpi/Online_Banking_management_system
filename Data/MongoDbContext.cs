using MongoDB.Driver;
using OnlineBankingSystem.Models;

namespace OnlineBankingSystem.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    private readonly IMongoClient _client;

    public MongoDbContext(IConfiguration configuration)
    {
        var connectionString = configuration.GetSection("MongoDbSettings:ConnectionString").Value;
        var databaseName = configuration.GetSection("MongoDbSettings:DatabaseName").Value ?? "onlinebank";

        var settings = MongoClientSettings.FromConnectionString(connectionString);
        settings.ServerApi = new ServerApi(ServerApiVersion.V1);
        _client = new MongoClient(settings);

        _database = _client.GetDatabase(databaseName);
    }

    public IMongoClient Client => _client;

    public IMongoCollection<Account> Accounts =>
        _database.GetCollection<Account>("Accounts");

    public IMongoCollection<Transaction> Transactions =>
        _database.GetCollection<Transaction>("Transactions");

    public IMongoCollection<AppUser> Users =>
        _database.GetCollection<AppUser>("Users");
}

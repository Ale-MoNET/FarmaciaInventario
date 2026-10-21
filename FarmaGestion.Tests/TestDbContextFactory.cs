using System;
using System.Threading;
using System.Threading.Tasks;
using FarmaGestion.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FarmaGestion.Tests;

/// <summary>
/// Fábrica de contextos para pruebas. Usa una base SQLite en memoria compartida
/// (la conexión se mantiene abierta mientras viva la fábrica) para que todas las
/// operaciones vean los mismos datos sin tocar disco.
/// </summary>
public sealed class TestDbContextFactory : IDbContextFactory<InventarioContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<InventarioContext> _options;

    public TestDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<InventarioContext>()
            .UseSqlite(_connection)
            .Options;

        // Crear el esquema una sola vez a partir del modelo.
        using var context = new InventarioContext(_options);
        context.Database.EnsureCreated();
    }

    public InventarioContext CreateDbContext() => new(_options);

    public Task<InventarioContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());

    public void Dispose() => _connection.Dispose();
}

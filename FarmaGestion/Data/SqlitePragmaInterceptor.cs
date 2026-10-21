using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FarmaGestion.Data;

/// <summary>
/// Ajusta SQLite para funcionar de forma segura en una carpeta compartida (red):
/// - Desactiva WAL (journal_mode = DELETE), porque WAL no funciona sobre SMB/red.
/// - Establece un busy_timeout para esperar si otra computadora tiene la base ocupada,
///   en lugar de fallar inmediatamente por bloqueo.
/// Se aplica en cada apertura de conexión.
/// </summary>
public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        AplicarPragmas(connection);
        base.ConnectionOpened(connection, eventData);
    }

    private static void AplicarPragmas(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        // 30 segundos de espera ante bloqueos + modo de journal compatible con red.
        cmd.CommandText = "PRAGMA busy_timeout = 30000; PRAGMA journal_mode = DELETE;";
        cmd.ExecuteNonQuery();
    }
}

using Microsoft.Data.SqlClient;
using System.Data;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Infraestructura.Persistencia;

public sealed class ItemLoteDocumentoRepositorioSql(IConfiguration configuracion) : IItemLoteDocumentoRepositorio
{
    private const string MensajeSinCabecera = "El procedimiento almacenado no devolvió el resultado esperado.";

    private string CadenaConexion => configuracion.GetConnectionString("MsFacturacion")
        ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'MsFacturacion'.");

    public async Task<ResultadoOperacion<int>> ActualizarEstadoSunatTodosAsync(
        string usuarioEjecutor, int idInquilino, int idLoteDocumento, string estadoItemCodigo,
        string? sunatCodigoRespuesta, string? sunatDescripcionRespuesta, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_ItemLoteDocumento_ActualizarEstadoSunatTodos", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdLoteDocumento", idLoteDocumento);
            comando.Parameters.AddWithValue("@vchEstadoItemCodigo", estadoItemCodigo);
            comando.Parameters.AddWithValue("@vchSunatCodigoRespuesta", (object?)sunatCodigoRespuesta ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchSunatDescripcionRespuesta", (object?)sunatDescripcionRespuesta ?? DBNull.Value);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idLote = lector.GetInt32(lector.GetOrdinal("IdLoteDocumento"));

            return ResultadoOperacion<int>.DeExito(mensaje, idLote);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    private static async Task<(TipoMensaje IdTipoMensaje, string Mensaje)> LeerCabeceraAsync(
        SqlDataReader lector, CancellationToken cancellationToken)
    {
        if (!await lector.ReadAsync(cancellationToken))
        {
            return (TipoMensaje.ErrorSistema, MensajeSinCabecera);
        }

        var idTipoMensaje = (TipoMensaje)lector.GetInt32(lector.GetOrdinal("IdTipoMensaje"));
        var mensaje = lector.GetString(lector.GetOrdinal("Mensaje"));
        return (idTipoMensaje, mensaje);
    }
}

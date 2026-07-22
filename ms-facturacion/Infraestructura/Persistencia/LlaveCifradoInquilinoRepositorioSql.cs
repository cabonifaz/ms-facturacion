using Microsoft.Data.SqlClient;
using System.Data;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Persistencia;

public sealed class LlaveCifradoInquilinoRepositorioSql(IConfiguration configuracion) : ILlaveCifradoInquilinoRepositorio
{
    private const string MensajeSinCabecera = "El procedimiento almacenado no devolvió el resultado esperado.";

    private string CadenaConexion => configuracion.GetConnectionString("MsFacturacion")
        ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'MsFacturacion'.");

    public async Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int versionLlave, byte[] llaveDatosCifrada, byte[] nonce, byte[] tag,
        string algoritmo, bool activo, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_LlaveCifradoInquilino_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intVersionLlave", versionLlave);
            comando.Parameters.Add("@varbinLlaveDatosCifrada", SqlDbType.VarBinary).Value = llaveDatosCifrada;
            comando.Parameters.Add("@varbinNonce", SqlDbType.VarBinary, 12).Value = nonce;
            comando.Parameters.Add("@varbinTag", SqlDbType.VarBinary, 16).Value = tag;
            comando.Parameters.AddWithValue("@vchAlgoritmo", algoritmo);
            comando.Parameters.AddWithValue("@bitActivo", activo);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idLlave = lector.GetInt32(lector.GetOrdinal("IdLlaveCifradoInquilino"));

            return ResultadoOperacion<int>.DeExito(mensaje, idLlave);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<LlaveCifradoInquilino>> ObtenerActivaAsync(int idInquilino, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_LlaveCifradoInquilino_ObtenerActiva", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            return await LeerLlaveAsync(lector, cancellationToken);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<LlaveCifradoInquilino>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<LlaveCifradoInquilino>> ObtenerPorVersionAsync(
        int idInquilino, int versionLlave, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_LlaveCifradoInquilino_ObtenerPorVersion", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intVersionLlave", versionLlave);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            return await LeerLlaveAsync(lector, cancellationToken);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<LlaveCifradoInquilino>.DeErrorSistema(ex.Message);
        }
    }

    private static async Task<ResultadoOperacion<LlaveCifradoInquilino>> LeerLlaveAsync(
        SqlDataReader lector, CancellationToken cancellationToken)
    {
        var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
        if (idTipoMensaje != TipoMensaje.Exito)
        {
            return new ResultadoOperacion<LlaveCifradoInquilino>(idTipoMensaje, mensaje, default);
        }

        await lector.NextResultAsync(cancellationToken);
        await lector.ReadAsync(cancellationToken);

        var llave = new LlaveCifradoInquilino(
            lector.GetInt32(lector.GetOrdinal("IdLlaveCifradoInquilino")),
            lector.GetInt32(lector.GetOrdinal("VersionLlave")),
            (byte[])lector["LlaveDatosCifrada"],
            (byte[])lector["Nonce"],
            (byte[])lector["Tag"],
            lector.GetString(lector.GetOrdinal("Algoritmo")));

        return ResultadoOperacion<LlaveCifradoInquilino>.DeExito(mensaje, llave);
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

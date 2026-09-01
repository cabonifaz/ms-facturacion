using MySqlConnector;
using System.Data;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Persistencia;

public sealed class ArchivoDocumentoRepositorioSql(IConfiguration configuracion) : IArchivoDocumentoRepositorio
{
    private const string MensajeSinCabecera = "El procedimiento almacenado no devolvió el resultado esperado.";

    private string CadenaConexion => configuracion.GetConnectionString("MsFacturacion")
        ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'MsFacturacion'.");

    public async Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, ArchivoDocumento archivo, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_ArchivoDocumento_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdDocumentoElectronico", (object?)archivo.IdDocumentoElectronico ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_intIdLoteDocumento", (object?)archivo.IdLoteDocumento ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchTipoArchivoCodigo", archivo.TipoArchivoCodigo);
            comando.Parameters.AddWithValue("@p_intIdTransmisionSunat", (object?)archivo.IdTransmisionSunat ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchNombreArchivo", archivo.NombreArchivo);
            comando.Parameters.AddWithValue("@p_vchRutaAlmacenamiento", archivo.RutaAlmacenamiento);
            comando.Parameters.AddWithValue("@p_vchTipoContenido", archivo.TipoContenido);
            comando.Parameters.AddWithValue("@p_chrHashSha256", archivo.HashSha256);
            comando.Parameters.AddWithValue("@p_bigTamanoBytes", archivo.TamanoBytes);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idArchivo = lector.GetInt32(lector.GetOrdinal("IdArchivoDocumento"));

            return ResultadoOperacion<int>.DeExito(mensaje, idArchivo);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<ArchivoDescarga>> ObtenerXmlOPdfAsync(
        int idInquilino, int idDocumentoElectronico, string tipoArchivoCodigo, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_ArchivoDocumento_ObtenerXmlYPdf", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdDocumentoElectronico", idDocumentoElectronico);
            comando.Parameters.AddWithValue("@p_vchTipoArchivoCodigo", tipoArchivoCodigo);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<ArchivoDescarga>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var archivo = new ArchivoDescarga(
                lector.GetString(lector.GetOrdinal("NombreArchivo")),
                lector.GetString(lector.GetOrdinal("RutaAlmacenamiento")),
                lector.GetString(lector.GetOrdinal("TipoContenido")));

            return ResultadoOperacion<ArchivoDescarga>.DeExito(mensaje, archivo);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<ArchivoDescarga>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<ArchivoDescarga>> ObtenerXmlOPdfPorTokenAsync(
        string tokenPublico, string tipoArchivoCodigo, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_ArchivoDocumento_ObtenerXmlYPdfPorToken", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchTokenPublico", tokenPublico);
            comando.Parameters.AddWithValue("@p_vchTipoArchivoCodigo", tipoArchivoCodigo);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<ArchivoDescarga>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var archivo = new ArchivoDescarga(
                lector.GetString(lector.GetOrdinal("NombreArchivo")),
                lector.GetString(lector.GetOrdinal("RutaAlmacenamiento")),
                lector.GetString(lector.GetOrdinal("TipoContenido")));

            return ResultadoOperacion<ArchivoDescarga>.DeExito(mensaje, archivo);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<ArchivoDescarga>.DeErrorSistema(ex.Message);
        }
    }

    private static async Task<(TipoMensaje IdTipoMensaje, string Mensaje)> LeerCabeceraAsync(
        MySqlDataReader lector, CancellationToken cancellationToken)
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

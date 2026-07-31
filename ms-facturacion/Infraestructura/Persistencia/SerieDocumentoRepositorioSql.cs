using Microsoft.Data.SqlClient;
using System.Data;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Persistencia;

public sealed class SerieDocumentoRepositorioSql(IConfiguration configuracion) : ISerieDocumentoRepositorio
{
    private const string MensajeSinCabecera = "El procedimiento almacenado no devolvió el resultado esperado.";

    private string CadenaConexion => configuracion.GetConnectionString("MsFacturacion")
        ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'MsFacturacion'.");

    public async Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, int idTipoDocumentoMaestro, string serie,
        int numeroActual, bool activo, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_SerieDocumento_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@intIdTipoDocumentoMaestro", idTipoDocumentoMaestro);
            comando.Parameters.AddWithValue("@vchSerie", serie);
            comando.Parameters.AddWithValue("@intNumeroActual", numeroActual);
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
            var idSerieDocumento = lector.GetInt32(lector.GetOrdinal("IdSerieDocumento"));

            return ResultadoOperacion<int>.DeExito(mensaje, idSerieDocumento);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<SerieDocumento>> ObtenerAsync(
        int idInquilino, int idSerieDocumento, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_SerieDocumento_Obtener", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdSerieDocumento", idSerieDocumento);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<SerieDocumento>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var serieDocumento = new SerieDocumento
            {
                IdSerieDocumento = lector.GetInt32(lector.GetOrdinal("IdSerieDocumento")),
                IdInquilino = lector.GetInt32(lector.GetOrdinal("IdInquilino")),
                IdEmpresa = lector.GetInt32(lector.GetOrdinal("IdEmpresa")),
                TipoDocumentoCodigo = lector.GetString(lector.GetOrdinal("TipoDocumentoCodigo")),
                Serie = lector.GetString(lector.GetOrdinal("Serie")),
                NumeroActual = lector.GetInt32(lector.GetOrdinal("NumeroActual")),
                Activo = lector.GetBoolean(lector.GetOrdinal("Activo")),
                FchCre = lector.GetDateTime(lector.GetOrdinal("FchCre")),
                FchMod = lector.IsDBNull(lector.GetOrdinal("FchMod")) ? null : lector.GetDateTime(lector.GetOrdinal("FchMod"))
            };

            return ResultadoOperacion<SerieDocumento>.DeExito(mensaje, serieDocumento);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<SerieDocumento>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<ResultadoPaginado<SerieDocumentoResumen>>> ListarAsync(
        int idInquilino, int idEmpresa, string? busqueda, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_SerieDocumento_Listar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@vchBusqueda", (object?)busqueda ?? DBNull.Value);
            comando.Parameters.AddWithValue("@numPag", numeroPagina);
            comando.Parameters.AddWithValue("@intTamPag", tamanoPagina);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<ResultadoPaginado<SerieDocumentoResumen>>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var totalRegistros = lector.GetInt32(lector.GetOrdinal("TotalRegistros"));
            var totalPaginas = lector.GetInt32(lector.GetOrdinal("TotalPaginas"));

            await lector.NextResultAsync(cancellationToken);
            var items = new List<SerieDocumentoResumen>();
            while (await lector.ReadAsync(cancellationToken))
            {
                items.Add(new SerieDocumentoResumen(
                    lector.GetInt32(lector.GetOrdinal("IdSerieDocumento")),
                    lector.GetString(lector.GetOrdinal("TipoDocumentoCodigo")),
                    lector.GetString(lector.GetOrdinal("Serie")),
                    lector.GetInt32(lector.GetOrdinal("NumeroActual")),
                    lector.GetBoolean(lector.GetOrdinal("Activo"))));
            }

            var paginado = new ResultadoPaginado<SerieDocumentoResumen>(totalRegistros, totalPaginas, items);
            return ResultadoOperacion<ResultadoPaginado<SerieDocumentoResumen>>.DeExito(mensaje, paginado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<ResultadoPaginado<SerieDocumentoResumen>>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<int>> ActualizarAsync(
        string usuarioEjecutor, int idInquilino, int idSerieDocumento, int idTipoDocumentoMaestro, string serie,
        int numeroActual, bool activo, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_SerieDocumento_Actualizar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdSerieDocumento", idSerieDocumento);
            comando.Parameters.AddWithValue("@intIdTipoDocumentoMaestro", idTipoDocumentoMaestro);
            comando.Parameters.AddWithValue("@vchSerie", serie);
            comando.Parameters.AddWithValue("@intNumeroActual", numeroActual);
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
            var idActualizado = lector.GetInt32(lector.GetOrdinal("IdSerieDocumento"));

            return ResultadoOperacion<int>.DeExito(mensaje, idActualizado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<int>> EliminarAsync(
        string usuarioEjecutor, int idInquilino, int idSerieDocumento, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_SerieDocumento_Eliminar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdSerieDocumento", idSerieDocumento);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idEliminado = lector.GetInt32(lector.GetOrdinal("IdSerieDocumento"));

            return ResultadoOperacion<int>.DeExito(mensaje, idEliminado);
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

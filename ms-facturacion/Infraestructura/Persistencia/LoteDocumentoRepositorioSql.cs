using Microsoft.Data.SqlClient;
using System.Data;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Persistencia;

public sealed class LoteDocumentoRepositorioSql(IConfiguration configuracion) : ILoteDocumentoRepositorio
{
    private const string MensajeSinCabecera = "El procedimiento almacenado no devolvió el resultado esperado.";

    private string CadenaConexion => configuracion.GetConnectionString("MsFacturacion")
        ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'MsFacturacion'.");

    public async Task<ResultadoOperacion<LoteDocumentoCreado>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, DateOnly fechaReferencia,
        IReadOnlyList<ItemBajaEntrada> items, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_LoteDocumento_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@dtFechaReferencia", fechaReferencia.ToDateTime(TimeOnly.MinValue));

            var tabla = new DataTable();
            tabla.Columns.Add("IdDocumentoElectronico", typeof(int));
            tabla.Columns.Add("MotivoDescripcion", typeof(string));
            foreach (var item in items)
            {
                tabla.Rows.Add(item.IdDocumentoElectronico, item.MotivoDescripcion);
            }

            var tvpItems = comando.Parameters.Add("@tvpItems", SqlDbType.Structured);
            tvpItems.TypeName = "dbo.TVP_ITEM_LOTE_DOCUMENTO_BAJA";
            tvpItems.Value = tabla;

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<LoteDocumentoCreado>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var creado = new LoteDocumentoCreado(
                lector.GetInt32(lector.GetOrdinal("IdLoteDocumento")),
                lector.GetString(lector.GetOrdinal("Nombre")),
                lector.GetString(lector.GetOrdinal("EstadoCodigo")),
                lector.GetDateTime(lector.GetOrdinal("FechaGeneracion")));

            return ResultadoOperacion<LoteDocumentoCreado>.DeExito(mensaje, creado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<LoteDocumentoCreado>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<LoteDocumentoDetalle>> ObtenerAsync(
        int idInquilino, int idLoteDocumento, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_LoteDocumento_Obtener", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdLoteDocumento", idLoteDocumento);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<LoteDocumentoDetalle>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var cabecera = new LoteDocumento(
                lector.GetInt32(lector.GetOrdinal("IdLoteDocumento")),
                lector.GetInt32(lector.GetOrdinal("IdEmpresa")),
                lector.GetString(lector.GetOrdinal("TipoLoteCodigo")),
                lector.GetString(lector.GetOrdinal("Nombre")),
                DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("FechaReferencia"))),
                lector.GetDateTime(lector.GetOrdinal("FechaGeneracion")),
                lector.GetString(lector.GetOrdinal("EstadoCodigo")),
                LeerNullableString(lector, "Ticket"),
                LeerNullableString(lector, "SunatCodigoRespuesta"),
                LeerNullableString(lector, "SunatDescripcionRespuesta"));

            await lector.NextResultAsync(cancellationToken);
            var items = new List<ItemLoteDocumentoDetalle>();
            while (await lector.ReadAsync(cancellationToken))
            {
                items.Add(new ItemLoteDocumentoDetalle(
                    lector.GetInt32(lector.GetOrdinal("IdItemLoteDocumento")),
                    lector.GetInt32(lector.GetOrdinal("IdDocumentoElectronico")),
                    lector.GetInt32(lector.GetOrdinal("NumeroLinea")),
                    lector.GetString(lector.GetOrdinal("MotivoDescripcion")),
                    lector.GetString(lector.GetOrdinal("EstadoItemCodigo")),
                    lector.GetString(lector.GetOrdinal("TipoDocumentoCodigo")),
                    lector.GetString(lector.GetOrdinal("Serie")),
                    lector.GetInt32(lector.GetOrdinal("Correlativo"))));
            }

            var detalle = new LoteDocumentoDetalle(cabecera, items);
            return ResultadoOperacion<LoteDocumentoDetalle>.DeExito(mensaje, detalle);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<LoteDocumentoDetalle>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<int>> ActualizarEstadoSunatAsync(
        string usuarioEjecutor, int idInquilino, int idLoteDocumento, EstadoMaestroCodigo estadoCodigo, string? ticket,
        string? sunatCodigoRespuesta, string? sunatDescripcionRespuesta, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_LoteDocumento_ActualizarEstadoSunat", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdLoteDocumento", idLoteDocumento);
            comando.Parameters.AddWithValue("@intEstadoCodigo", (int)estadoCodigo);
            comando.Parameters.AddWithValue("@vchTicket", (object?)ticket ?? DBNull.Value);
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
            var idActualizado = lector.GetInt32(lector.GetOrdinal("IdLoteDocumento"));

            return ResultadoOperacion<int>.DeExito(mensaje, idActualizado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<IReadOnlyList<LotePendienteTicket>>> ListarPendientesTicketAsync(int tamanoPagina, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_LoteDocumento_ListarPendientesTicket", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intTamPag", tamanoPagina);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<IReadOnlyList<LotePendienteTicket>>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            var lotes = new List<LotePendienteTicket>();
            while (await lector.ReadAsync(cancellationToken))
            {
                lotes.Add(new LotePendienteTicket(
                    lector.GetInt32(lector.GetOrdinal("IdInquilino")),
                    lector.GetInt32(lector.GetOrdinal("IdLoteDocumento")),
                    LeerNullableString(lector, "Ticket")));
            }

            return ResultadoOperacion<IReadOnlyList<LotePendienteTicket>>.DeExito(mensaje, lotes);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<IReadOnlyList<LotePendienteTicket>>.DeErrorSistema(ex.Message);
        }
    }

    private static string? LeerNullableString(SqlDataReader lector, string columna)
    {
        var ordinal = lector.GetOrdinal(columna);
        return lector.IsDBNull(ordinal) ? null : lector.GetString(ordinal);
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

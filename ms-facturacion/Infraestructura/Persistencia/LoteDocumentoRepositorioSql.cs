using MySqlConnector;
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
        string usuarioEjecutor, int idInquilino, int idEmpresa, DateOnly fechaReferencia, DateOnly fechaGeneracion,
        IReadOnlyList<ItemBajaEntrada> items, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_LoteDocumento_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_dtFechaReferencia", fechaReferencia.ToDateTime(TimeOnly.MinValue));
            comando.Parameters.AddWithValue("@p_dtFechaGeneracion", fechaGeneracion.ToDateTime(TimeOnly.MinValue));

            var jsonItems = items.Select(item => new { item.IdDocumentoElectronico, item.MotivoDescripcion });
            comando.Parameters.AddWithValue("@p_jsonItems", System.Text.Json.JsonSerializer.Serialize(jsonItems));

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

    public async Task<ResultadoOperacion<IReadOnlyList<DocumentoBajaPreview>>> PrevisualizarBajaAsync(
        int idInquilino, int idEmpresa, DateOnly fechaGeneracion,
        IReadOnlyList<int> idsDocumentoElectronico, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_LoteDocumento_PrevisualizarBaja", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_dtFechaGeneracion", fechaGeneracion.ToDateTime(TimeOnly.MinValue));

            // MotivoDescripcion es NOT NULL en TVP_ITEM_LOTE_DOCUMENTO_BAJA (mismo tipo que InsertarAsync),
            // pero SP_LoteDocumento_PrevisualizarBaja nunca lo lee — placeholder solo para cumplir el shape.
            var jsonItems = idsDocumentoElectronico.Select(id => new { IdDocumentoElectronico = id, MotivoDescripcion = string.Empty });
            comando.Parameters.AddWithValue("@p_jsonItems", System.Text.Json.JsonSerializer.Serialize(jsonItems));

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<IReadOnlyList<DocumentoBajaPreview>>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);

            var afectados = new List<DocumentoBajaPreview>();
            while (await lector.ReadAsync(cancellationToken))
            {
                afectados.Add(new DocumentoBajaPreview(
                    lector.GetInt32(lector.GetOrdinal("IdDocumentoElectronico")),
                    lector.GetString(lector.GetOrdinal("TipoDocumentoCodigo")),
                    lector.GetString(lector.GetOrdinal("NumeroDocumento")),
                    DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("FechaEmision"))),
                    lector.GetString(lector.GetOrdinal("EstadoCodigo"))));
            }

            return ResultadoOperacion<IReadOnlyList<DocumentoBajaPreview>>.DeExito(mensaje, afectados);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<IReadOnlyList<DocumentoBajaPreview>>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<LoteDocumentoCreado>> InsertarResumenBajaBoletaAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, DateOnly fechaReferencia, DateOnly fechaGeneracion,
        IReadOnlyList<ItemBajaEntrada> items, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_LoteResumenBajaBoleta_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_dtFechaReferencia", fechaReferencia.ToDateTime(TimeOnly.MinValue));
            comando.Parameters.AddWithValue("@p_dtFechaGeneracion", fechaGeneracion.ToDateTime(TimeOnly.MinValue));

            var jsonItems = items.Select(item => new { item.IdDocumentoElectronico, item.MotivoDescripcion });
            comando.Parameters.AddWithValue("@p_jsonItems", System.Text.Json.JsonSerializer.Serialize(jsonItems));

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

    public async Task<ResultadoOperacion<IReadOnlyList<DocumentoBajaPreview>>> PrevisualizarResumenBajaBoletaAsync(
        int idInquilino, int idEmpresa, DateOnly fechaGeneracion,
        IReadOnlyList<int> idsDocumentoElectronico, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_LoteResumenBajaBoleta_PrevisualizarBaja", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_dtFechaGeneracion", fechaGeneracion.ToDateTime(TimeOnly.MinValue));

            var jsonItems = idsDocumentoElectronico.Select(id => new { IdDocumentoElectronico = id, MotivoDescripcion = string.Empty });
            comando.Parameters.AddWithValue("@p_jsonItems", System.Text.Json.JsonSerializer.Serialize(jsonItems));

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<IReadOnlyList<DocumentoBajaPreview>>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);

            var afectados = new List<DocumentoBajaPreview>();
            while (await lector.ReadAsync(cancellationToken))
            {
                afectados.Add(new DocumentoBajaPreview(
                    lector.GetInt32(lector.GetOrdinal("IdDocumentoElectronico")),
                    lector.GetString(lector.GetOrdinal("TipoDocumentoCodigo")),
                    lector.GetString(lector.GetOrdinal("NumeroDocumento")),
                    DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("FechaEmision"))),
                    lector.GetString(lector.GetOrdinal("EstadoCodigo"))));
            }

            return ResultadoOperacion<IReadOnlyList<DocumentoBajaPreview>>.DeExito(mensaje, afectados);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<IReadOnlyList<DocumentoBajaPreview>>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<LoteDocumentoCreado>> InsertarManualAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, IReadOnlyList<ItemBajaEntrada> items,
        DateOnly fechaReferencia, DateTime fechaGeneracion, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_LoteDocumento_InsertarManual", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_dtFechaReferencia", fechaReferencia.ToDateTime(TimeOnly.MinValue));
            comando.Parameters.AddWithValue("@p_dtFechaGeneracion", fechaGeneracion);

            var jsonItems = items.Select(item => new { item.IdDocumentoElectronico, item.MotivoDescripcion });
            comando.Parameters.AddWithValue("@p_jsonItems", System.Text.Json.JsonSerializer.Serialize(jsonItems));

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
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_LoteDocumento_Obtener", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdLoteDocumento", idLoteDocumento);

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
                    lector.GetInt32(lector.GetOrdinal("Correlativo")),
                    lector.GetDecimal(lector.GetOrdinal("TotalImporte")),
                    lector.GetDecimal(lector.GetOrdinal("TotalIgv")),
                    lector.GetString(lector.GetOrdinal("MonedaCodigo"))));
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
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_LoteDocumento_ActualizarEstadoSunat", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdLoteDocumento", idLoteDocumento);
            comando.Parameters.AddWithValue("@p_intEstadoCodigo", (int)estadoCodigo);
            comando.Parameters.AddWithValue("@p_vchTicket", (object?)ticket ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchSunatCodigoRespuesta", (object?)sunatCodigoRespuesta ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchSunatDescripcionRespuesta", (object?)sunatDescripcionRespuesta ?? DBNull.Value);

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

    public async Task<ResultadoOperacion<IReadOnlyList<LotePendienteTicket>>> ListarPendientesTicketAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_LoteDocumento_ListarPendientesTicket", conexion) { CommandType = CommandType.StoredProcedure };

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
                    lector.GetString(lector.GetOrdinal("TipoLoteCodigo")),
                    LeerNullableString(lector, "Ticket")));
            }

            return ResultadoOperacion<IReadOnlyList<LotePendienteTicket>>.DeExito(mensaje, lotes);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<IReadOnlyList<LotePendienteTicket>>.DeErrorSistema(ex.Message);
        }
    }

    private static string? LeerNullableString(MySqlDataReader lector, string columna)
    {
        var ordinal = lector.GetOrdinal(columna);
        return lector.IsDBNull(ordinal) ? null : lector.GetString(ordinal);
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

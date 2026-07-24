using Microsoft.Data.SqlClient;
using System.Data;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Persistencia;

public sealed class DocumentoElectronicoRepositorioSql(IConfiguration configuracion) : IDocumentoElectronicoRepositorio
{
    private const string MensajeSinCabecera = "El procedimiento almacenado no devolvió el resultado esperado.";

    private string CadenaConexion => configuracion.GetConnectionString("MsFacturacion")
        ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'MsFacturacion'.");

    public async Task<ResultadoOperacion<DocumentoElectronicoCreado>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string sistemaOrigen, string idExterno,
        string tipoDocumentoCodigo, int idSerieDocumento, DateOnly fechaEmision, TimeOnly horaEmision,
        string monedaCodigo, string tipoOperacionCodigo, string formaPagoCodigo, ClienteDatosEntrada cliente,
        DocumentoAfectadoEntrada? documentoAfectado, IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas,
        IReadOnlyList<CuotaDocumentoElectronico> cuotas, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_DocumentoElectronico_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@vchSistemaOrigen", sistemaOrigen);
            comando.Parameters.AddWithValue("@vchIdExterno", idExterno);
            comando.Parameters.AddWithValue("@vchTipoDocumentoCodigo", tipoDocumentoCodigo);
            comando.Parameters.AddWithValue("@intIdSerieDocumento", idSerieDocumento);
            comando.Parameters.AddWithValue("@dtFechaEmision", fechaEmision.ToDateTime(TimeOnly.MinValue));
            comando.Parameters.Add("@tmHoraEmision", SqlDbType.Time).Value = horaEmision.ToTimeSpan();
            comando.Parameters.AddWithValue("@chrMonedaCodigo", monedaCodigo);
            comando.Parameters.AddWithValue("@vchTipoOperacionCodigo", tipoOperacionCodigo);
            comando.Parameters.AddWithValue("@vchFormaPagoCodigo", formaPagoCodigo);
            comando.Parameters.AddWithValue("@vchClienteTipoDocumentoCodigo", cliente.TipoDocumentoCodigo);
            comando.Parameters.AddWithValue("@vchClienteNumeroDocumento", cliente.NumeroDocumento);
            comando.Parameters.AddWithValue("@vchClienteNombre", (object?)cliente.Nombre ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchClienteCorreo", (object?)cliente.Correo ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchClienteDireccion", (object?)cliente.Direccion ?? DBNull.Value);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronicoRelacionado", (object?)documentoAfectado?.IdDocumentoElectronicoRelacionado ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchTipoReferenciaCodigo", (object?)documentoAfectado?.TipoReferenciaCodigo ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchMotivoCodigo", (object?)documentoAfectado?.MotivoCodigo ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchMotivoDescripcion", (object?)documentoAfectado?.MotivoDescripcion ?? DBNull.Value);

            var tvpLineas = comando.Parameters.Add("@tvpLineas", SqlDbType.Structured);
            tvpLineas.TypeName = "dbo.TVP_LINEA_DOCUMENTO_ELECTRONICO";
            tvpLineas.Value = ConstruirTablaLineas(lineas);

            var tvpCuotas = comando.Parameters.Add("@tvpCuotas", SqlDbType.Structured);
            tvpCuotas.TypeName = "dbo.TVP_CUOTA_DOCUMENTO_ELECTRONICO";
            tvpCuotas.Value = ConstruirTablaCuotas(cuotas);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<DocumentoElectronicoCreado>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var creado = new DocumentoElectronicoCreado(
                lector.GetInt32(lector.GetOrdinal("IdDocumentoElectronico")),
                lector.GetString(lector.GetOrdinal("Serie")),
                lector.GetInt32(lector.GetOrdinal("Correlativo")),
                lector.GetString(lector.GetOrdinal("EstadoCodigo")),
                lector.GetDateTime(lector.GetOrdinal("FechaCreacion")));

            return ResultadoOperacion<DocumentoElectronicoCreado>.DeExito(mensaje, creado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<DocumentoElectronicoCreado>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<DocumentoElectronicoDetalle>> ObtenerAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_DocumentoElectronico_Obtener", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", idDocumentoElectronico);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<DocumentoElectronicoDetalle>(idTipoMensaje, mensaje, default);
            }

            // Result set 2: cabecera
            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var cabecera = new DocumentoElectronico
            {
                IdDocumentoElectronico = lector.GetInt32(lector.GetOrdinal("IdDocumentoElectronico")),
                IdEmpresa = lector.GetInt32(lector.GetOrdinal("IdEmpresa")),
                IdCliente = lector.GetInt32(lector.GetOrdinal("IdCliente")),
                IdExterno = lector.GetString(lector.GetOrdinal("IdExterno")),
                SistemaOrigen = lector.GetString(lector.GetOrdinal("SistemaOrigen")),
                TipoDocumentoCodigo = lector.GetString(lector.GetOrdinal("TipoDocumentoCodigo")),
                Serie = lector.GetString(lector.GetOrdinal("Serie")),
                Correlativo = lector.GetInt32(lector.GetOrdinal("Correlativo")),
                EstadoCodigo = lector.GetString(lector.GetOrdinal("EstadoCodigo")),
                FechaEmision = DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("FechaEmision"))),
                HoraEmision = TimeOnly.FromTimeSpan(lector.GetTimeSpan(lector.GetOrdinal("HoraEmision"))),
                MonedaCodigo = lector.GetString(lector.GetOrdinal("MonedaCodigo")),
                TipoOperacionCodigo = lector.GetString(lector.GetOrdinal("TipoOperacionCodigo")),
                EmpresaRuc = lector.GetString(lector.GetOrdinal("EmpresaRuc")),
                EmpresaRazonSocial = lector.GetString(lector.GetOrdinal("EmpresaRazonSocial")),
                EmpresaNombreComercial = LeerNullableString(lector, "EmpresaNombreComercial"),
                EmpresaDireccion = lector.GetString(lector.GetOrdinal("EmpresaDireccion")),
                EmpresaUbigeo = lector.GetString(lector.GetOrdinal("EmpresaUbigeo")),
                ClienteTipoDocumentoCodigo = lector.GetString(lector.GetOrdinal("ClienteTipoDocumentoCodigo")),
                ClienteNumeroDocumento = lector.GetString(lector.GetOrdinal("ClienteNumeroDocumento")),
                ClienteNombre = lector.GetString(lector.GetOrdinal("ClienteNombre")),
                ClienteDireccion = LeerNullableString(lector, "ClienteDireccion"),
                ClienteCorreo = LeerNullableString(lector, "ClienteCorreo"),
                ClientePaisCodigo = lector.GetString(lector.GetOrdinal("ClientePaisCodigo")),
                TotalGravado = lector.GetDecimal(lector.GetOrdinal("TotalGravado")),
                TotalInafecto = lector.GetDecimal(lector.GetOrdinal("TotalInafecto")),
                TotalExonerado = lector.GetDecimal(lector.GetOrdinal("TotalExonerado")),
                TotalGratuito = lector.GetDecimal(lector.GetOrdinal("TotalGratuito")),
                TotalIgv = lector.GetDecimal(lector.GetOrdinal("TotalIgv")),
                TotalIsc = lector.GetDecimal(lector.GetOrdinal("TotalIsc")),
                TotalOtrosTributos = lector.GetDecimal(lector.GetOrdinal("TotalOtrosTributos")),
                TotalDescuento = lector.GetDecimal(lector.GetOrdinal("TotalDescuento")),
                TotalCargo = lector.GetDecimal(lector.GetOrdinal("TotalCargo")),
                TotalImporte = lector.GetDecimal(lector.GetOrdinal("TotalImporte")),
                SunatHash = LeerNullableString(lector, "SunatHash"),
                SunatCodigoRespuesta = LeerNullableString(lector, "SunatCodigoRespuesta"),
                SunatDescripcionRespuesta = LeerNullableString(lector, "SunatDescripcionRespuesta"),
                SunatTicket = LeerNullableString(lector, "SunatTicket"),
                FechaAceptacion = LeerNullableDateTime(lector, "FechaAceptacion"),
                FechaRechazo = LeerNullableDateTime(lector, "FechaRechazo"),
                FechaAnulacion = LeerNullableDateTime(lector, "FechaAnulacion"),
                FchCre = lector.GetDateTime(lector.GetOrdinal("FchCre"))
            };

            // Result set 3: líneas
            await lector.NextResultAsync(cancellationToken);
            var lineas = new List<LineaDocumentoElectronico>();
            while (await lector.ReadAsync(cancellationToken))
            {
                lineas.Add(new LineaDocumentoElectronico(
                    lector.GetInt32(lector.GetOrdinal("IdLineaDocumentoElectronico")),
                    lector.GetInt32(lector.GetOrdinal("NumeroLinea")),
                    lector.GetString(lector.GetOrdinal("ProductoCodigo")),
                    LeerNullableString(lector, "ProductoSunatCodigo"),
                    lector.GetString(lector.GetOrdinal("Descripcion")),
                    lector.GetString(lector.GetOrdinal("UnidadMedidaCodigo")),
                    lector.GetDecimal(lector.GetOrdinal("Cantidad")),
                    lector.GetDecimal(lector.GetOrdinal("ValorUnitario")),
                    lector.GetDecimal(lector.GetOrdinal("PrecioUnitario")),
                    lector.GetDecimal(lector.GetOrdinal("MontoDescuento")),
                    lector.GetString(lector.GetOrdinal("AfectacionIgvCodigo")),
                    lector.GetDecimal(lector.GetOrdinal("PorcentajeIgv")),
                    lector.GetDecimal(lector.GetOrdinal("MontoIgv")),
                    lector.GetDecimal(lector.GetOrdinal("MontoIsc")),
                    lector.GetDecimal(lector.GetOrdinal("MontoOtrosTributos")),
                    lector.GetDecimal(lector.GetOrdinal("ValorLinea")),
                    lector.GetDecimal(lector.GetOrdinal("TotalLinea"))));
            }

            // Result set 4: referencia (0 o 1 fila — solo notas de crédito/débito)
            await lector.NextResultAsync(cancellationToken);
            ReferenciaDocumentoElectronico? referencia = null;
            if (await lector.ReadAsync(cancellationToken))
            {
                referencia = new ReferenciaDocumentoElectronico(
                    lector.IsDBNull(lector.GetOrdinal("IdDocumentoElectronicoRelacionado")) ? null : lector.GetInt32(lector.GetOrdinal("IdDocumentoElectronicoRelacionado")),
                    lector.GetString(lector.GetOrdinal("TipoDocumentoRelacionadoCodigo")),
                    lector.GetString(lector.GetOrdinal("SerieRelacionada")),
                    lector.GetInt32(lector.GetOrdinal("CorrelativoRelacionado")),
                    lector.GetString(lector.GetOrdinal("TipoReferenciaCodigo")),
                    lector.GetString(lector.GetOrdinal("MotivoCodigo")),
                    lector.GetString(lector.GetOrdinal("MotivoDescripcion")));
            }

            // Result set 5: cuotas (0 filas si fue Contado)
            await lector.NextResultAsync(cancellationToken);
            var cuotas = new List<CuotaDocumentoElectronico>();
            while (await lector.ReadAsync(cancellationToken))
            {
                cuotas.Add(new CuotaDocumentoElectronico(
                    lector.GetInt32(lector.GetOrdinal("NumeroCuota")),
                    DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("FechaVencimiento"))),
                    lector.GetDecimal(lector.GetOrdinal("Monto")),
                    lector.GetInt32(lector.GetOrdinal("IdCuotaDocumentoElectronico"))));
            }

            var detalle = new DocumentoElectronicoDetalle(cabecera, lineas, referencia, cuotas);
            return ResultadoOperacion<DocumentoElectronicoDetalle>.DeExito(mensaje, detalle);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<DocumentoElectronicoDetalle>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<ResultadoPaginado<DocumentoElectronicoResumen>>> ListarAsync(
        int idInquilino, int idEmpresa, string? estadoCodigo, string? busqueda, DateOnly? fechaDesde, DateOnly? fechaHasta,
        int numeroPagina, int tamanoPagina, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_DocumentoElectronico_Listar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@vchEstadoCodigo", (object?)estadoCodigo ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchBusqueda", (object?)busqueda ?? DBNull.Value);
            comando.Parameters.AddWithValue("@dtFechaDesde", (object?)fechaDesde?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
            comando.Parameters.AddWithValue("@dtFechaHasta", (object?)fechaHasta?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
            comando.Parameters.AddWithValue("@numPag", numeroPagina);
            comando.Parameters.AddWithValue("@intTamPag", tamanoPagina);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<ResultadoPaginado<DocumentoElectronicoResumen>>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var totalRegistros = lector.GetInt32(lector.GetOrdinal("TotalRegistros"));
            var totalPaginas = lector.GetInt32(lector.GetOrdinal("TotalPaginas"));

            await lector.NextResultAsync(cancellationToken);
            var items = new List<DocumentoElectronicoResumen>();
            while (await lector.ReadAsync(cancellationToken))
            {
                items.Add(new DocumentoElectronicoResumen(
                    lector.GetInt32(lector.GetOrdinal("IdDocumentoElectronico")),
                    lector.GetString(lector.GetOrdinal("TipoDocumentoCodigo")),
                    lector.GetString(lector.GetOrdinal("Serie")),
                    lector.GetInt32(lector.GetOrdinal("Correlativo")),
                    lector.GetString(lector.GetOrdinal("EstadoCodigo")),
                    lector.GetString(lector.GetOrdinal("ClienteNombre")),
                    lector.GetDecimal(lector.GetOrdinal("TotalImporte")),
                    DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("FechaEmision")))));
            }

            var paginado = new ResultadoPaginado<DocumentoElectronicoResumen>(totalRegistros, totalPaginas, items);
            return ResultadoOperacion<ResultadoPaginado<DocumentoElectronicoResumen>>.DeExito(mensaje, paginado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<ResultadoPaginado<DocumentoElectronicoResumen>>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<EstadoDocumentoElectronicoActualizado>> ActualizarEstadoSunatAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, string estadoCodigo, string? sunatHash,
        string? sunatCodigoRespuesta, string? sunatDescripcionRespuesta, string? sunatTicket, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_DocumentoElectronico_ActualizarEstadoSunat", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", idDocumentoElectronico);
            comando.Parameters.AddWithValue("@vchEstadoCodigo", estadoCodigo);
            comando.Parameters.AddWithValue("@vchSunatHash", (object?)sunatHash ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchSunatCodigoRespuesta", (object?)sunatCodigoRespuesta ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchSunatDescripcionRespuesta", (object?)sunatDescripcionRespuesta ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchSunatTicket", (object?)sunatTicket ?? DBNull.Value);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<EstadoDocumentoElectronicoActualizado>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var actualizado = new EstadoDocumentoElectronicoActualizado(
                lector.GetInt32(lector.GetOrdinal("IdDocumentoElectronico")),
                lector.GetString(lector.GetOrdinal("EstadoCodigo")));

            return ResultadoOperacion<EstadoDocumentoElectronicoActualizado>.DeExito(mensaje, actualizado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<EstadoDocumentoElectronicoActualizado>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<bool>> ActualizarFechaEmisionAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico,
        DateOnly fechaEmision, TimeOnly horaEmision, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_DocumentoElectronico_ActualizarFechaEmision", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", idDocumentoElectronico);
            comando.Parameters.AddWithValue("@dtmFechaEmision", fechaEmision.ToDateTime(TimeOnly.MinValue));
            comando.Parameters.Add("@timHoraEmision", SqlDbType.Time).Value = horaEmision.ToTimeSpan();

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            return idTipoMensaje == TipoMensaje.Exito
                ? ResultadoOperacion<bool>.DeExito(mensaje, true)
                : new ResultadoOperacion<bool>(idTipoMensaje, mensaje, default);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<bool>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<LineaDocumentoElectronico>> AgregarLineaAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico,
        LineaDocumentoElectronicoEntrada linea, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_LineaDocumentoElectronico_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", idDocumentoElectronico);
            AgregarParametrosLinea(comando, linea);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<LineaDocumentoElectronico>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            return ResultadoOperacion<LineaDocumentoElectronico>.DeExito(mensaje, LeerLinea(lector));
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<LineaDocumentoElectronico>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<LineaDocumentoElectronico>> ActualizarLineaAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, int idLineaDocumentoElectronico,
        LineaDocumentoElectronicoEntrada linea, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_LineaDocumentoElectronico_Actualizar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", idDocumentoElectronico);
            comando.Parameters.AddWithValue("@intIdLineaDocumentoElectronico", idLineaDocumentoElectronico);
            AgregarParametrosLinea(comando, linea);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<LineaDocumentoElectronico>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            return ResultadoOperacion<LineaDocumentoElectronico>.DeExito(mensaje, LeerLinea(lector));
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<LineaDocumentoElectronico>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<bool>> EliminarLineaAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, int idLineaDocumentoElectronico,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_LineaDocumentoElectronico_Eliminar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", idDocumentoElectronico);
            comando.Parameters.AddWithValue("@intIdLineaDocumentoElectronico", idLineaDocumentoElectronico);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            return idTipoMensaje == TipoMensaje.Exito
                ? ResultadoOperacion<bool>.DeExito(mensaje, true)
                : new ResultadoOperacion<bool>(idTipoMensaje, mensaje, default);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<bool>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<CuotaDocumentoElectronico>> AgregarCuotaAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico,
        DateOnly fechaVencimiento, decimal monto, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_CuotaDocumentoElectronico_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", idDocumentoElectronico);
            comando.Parameters.AddWithValue("@dtmFechaVencimiento", fechaVencimiento.ToDateTime(TimeOnly.MinValue));
            comando.Parameters.AddWithValue("@decMonto", monto);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<CuotaDocumentoElectronico>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            return ResultadoOperacion<CuotaDocumentoElectronico>.DeExito(mensaje, LeerCuota(lector));
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<CuotaDocumentoElectronico>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<CuotaDocumentoElectronico>> ActualizarCuotaAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, int idCuotaDocumentoElectronico,
        DateOnly fechaVencimiento, decimal monto, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_CuotaDocumentoElectronico_Actualizar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", idDocumentoElectronico);
            comando.Parameters.AddWithValue("@intIdCuotaDocumentoElectronico", idCuotaDocumentoElectronico);
            comando.Parameters.AddWithValue("@dtmFechaVencimiento", fechaVencimiento.ToDateTime(TimeOnly.MinValue));
            comando.Parameters.AddWithValue("@decMonto", monto);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<CuotaDocumentoElectronico>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            return ResultadoOperacion<CuotaDocumentoElectronico>.DeExito(mensaje, LeerCuota(lector));
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<CuotaDocumentoElectronico>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<bool>> EliminarCuotaAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, int idCuotaDocumentoElectronico,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_CuotaDocumentoElectronico_Eliminar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", idDocumentoElectronico);
            comando.Parameters.AddWithValue("@intIdCuotaDocumentoElectronico", idCuotaDocumentoElectronico);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            return idTipoMensaje == TipoMensaje.Exito
                ? ResultadoOperacion<bool>.DeExito(mensaje, true)
                : new ResultadoOperacion<bool>(idTipoMensaje, mensaje, default);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<bool>.DeErrorSistema(ex.Message);
        }
    }

    private static void AgregarParametrosLinea(SqlCommand comando, LineaDocumentoElectronicoEntrada linea)
    {
        comando.Parameters.AddWithValue("@vchProductoCodigo", linea.ProductoCodigo);
        comando.Parameters.AddWithValue("@vchProductoSunatCodigo", (object?)linea.ProductoSunatCodigo ?? DBNull.Value);
        comando.Parameters.AddWithValue("@vchDescripcion", linea.Descripcion);
        comando.Parameters.AddWithValue("@vchUnidadMedidaCodigo", linea.UnidadMedidaCodigo);
        comando.Parameters.AddWithValue("@decCantidad", linea.Cantidad);
        comando.Parameters.AddWithValue("@decValorUnitario", linea.ValorUnitario);
        comando.Parameters.AddWithValue("@decPrecioUnitario", linea.PrecioUnitario);
        comando.Parameters.AddWithValue("@decMontoDescuento", linea.MontoDescuento);
        comando.Parameters.AddWithValue("@vchAfectacionIgvCodigo", linea.AfectacionIgvCodigo);
        comando.Parameters.AddWithValue("@decPorcentajeIgv", linea.PorcentajeIgv);
    }

    private static LineaDocumentoElectronico LeerLinea(SqlDataReader lector) => new(
        lector.GetInt32(lector.GetOrdinal("IdLineaDocumentoElectronico")),
        lector.GetInt32(lector.GetOrdinal("NumeroLinea")),
        lector.GetString(lector.GetOrdinal("ProductoCodigo")),
        LeerNullableString(lector, "ProductoSunatCodigo"),
        lector.GetString(lector.GetOrdinal("Descripcion")),
        lector.GetString(lector.GetOrdinal("UnidadMedidaCodigo")),
        lector.GetDecimal(lector.GetOrdinal("Cantidad")),
        lector.GetDecimal(lector.GetOrdinal("ValorUnitario")),
        lector.GetDecimal(lector.GetOrdinal("PrecioUnitario")),
        lector.GetDecimal(lector.GetOrdinal("MontoDescuento")),
        lector.GetString(lector.GetOrdinal("AfectacionIgvCodigo")),
        lector.GetDecimal(lector.GetOrdinal("PorcentajeIgv")),
        lector.GetDecimal(lector.GetOrdinal("MontoIgv")),
        lector.GetDecimal(lector.GetOrdinal("MontoIsc")),
        lector.GetDecimal(lector.GetOrdinal("MontoOtrosTributos")),
        lector.GetDecimal(lector.GetOrdinal("ValorLinea")),
        lector.GetDecimal(lector.GetOrdinal("TotalLinea")));

    private static CuotaDocumentoElectronico LeerCuota(SqlDataReader lector) => new(
        lector.GetInt32(lector.GetOrdinal("NumeroCuota")),
        DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("FechaVencimiento"))),
        lector.GetDecimal(lector.GetOrdinal("Monto")),
        lector.GetInt32(lector.GetOrdinal("IdCuotaDocumentoElectronico")));

    /// El orden de columnas debe coincidir exactamente con TVP_LINEA_DOCUMENTO_ELECTRONICO (02_CrearTipos_MsFacturacion.sql) —
    /// una TVP basada en DataTable se mapea posicionalmente, no por nombre.
    private static DataTable ConstruirTablaLineas(IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("NumeroLinea", typeof(int));
        tabla.Columns.Add("ProductoCodigo", typeof(string));
        tabla.Columns.Add("ProductoSunatCodigo", typeof(string));
        tabla.Columns.Add("Descripcion", typeof(string));
        tabla.Columns.Add("UnidadMedidaCodigo", typeof(string));
        tabla.Columns.Add("Cantidad", typeof(decimal));
        tabla.Columns.Add("ValorUnitario", typeof(decimal));
        tabla.Columns.Add("PrecioUnitario", typeof(decimal));
        tabla.Columns.Add("MontoDescuento", typeof(decimal));
        tabla.Columns.Add("AfectacionIgvCodigo", typeof(string));
        tabla.Columns.Add("PorcentajeIgv", typeof(decimal));

        foreach (var linea in lineas)
        {
            tabla.Rows.Add(
                linea.NumeroLinea, linea.ProductoCodigo, (object?)linea.ProductoSunatCodigo ?? DBNull.Value,
                linea.Descripcion, linea.UnidadMedidaCodigo, linea.Cantidad, linea.ValorUnitario,
                linea.PrecioUnitario, linea.MontoDescuento, linea.AfectacionIgvCodigo, linea.PorcentajeIgv);
        }

        return tabla;
    }

    /// El orden de columnas debe coincidir exactamente con TVP_CUOTA_DOCUMENTO_ELECTRONICO.
    private static DataTable ConstruirTablaCuotas(IReadOnlyList<CuotaDocumentoElectronico> cuotas)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("NumeroCuota", typeof(int));
        tabla.Columns.Add("FechaVencimiento", typeof(DateTime));
        tabla.Columns.Add("Monto", typeof(decimal));

        foreach (var cuota in cuotas)
        {
            tabla.Rows.Add(cuota.NumeroCuota, cuota.FechaVencimiento.ToDateTime(TimeOnly.MinValue), cuota.Monto);
        }

        return tabla;
    }

    private static string? LeerNullableString(SqlDataReader lector, string columna)
    {
        var ordinal = lector.GetOrdinal(columna);
        return lector.IsDBNull(ordinal) ? null : lector.GetString(ordinal);
    }

    private static DateTime? LeerNullableDateTime(SqlDataReader lector, string columna)
    {
        var ordinal = lector.GetOrdinal(columna);
        return lector.IsDBNull(ordinal) ? null : lector.GetDateTime(ordinal);
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

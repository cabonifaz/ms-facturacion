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
        string usuarioEjecutor, int idInquilino, int idEmpresa, string idExterno, string? numeroReferencia,
        int idTipoDocumentoMaestro, DateOnly fechaEmision, TimeOnly horaEmision,
        int idMonedaMaestro, decimal? tipoCambio, int idTipoOperacionMaestro, int idFormaPago, ClienteDatosEntrada cliente,
        DocumentoAfectadoEntrada? documentoAfectado, IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas,
        IReadOnlyList<CuotaDocumentoElectronico> cuotas, IReadOnlyList<CampoExtraEntrada> camposExtra,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_DocumentoElectronico_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@vchIdExterno", idExterno);
            comando.Parameters.AddWithValue("@vchNumeroReferencia", (object?)numeroReferencia ?? DBNull.Value);
            comando.Parameters.AddWithValue("@intIdTipoDocumentoMaestro", idTipoDocumentoMaestro);
            comando.Parameters.AddWithValue("@dtFechaEmision", fechaEmision.ToDateTime(TimeOnly.MinValue));
            comando.Parameters.Add("@tmHoraEmision", SqlDbType.Time).Value = horaEmision.ToTimeSpan();
            comando.Parameters.AddWithValue("@intIdMonedaMaestro", idMonedaMaestro);
            comando.Parameters.AddWithValue("@decTipoCambio", (object?)tipoCambio ?? DBNull.Value);
            comando.Parameters.AddWithValue("@intIdTipoOperacionMaestro", idTipoOperacionMaestro);
            comando.Parameters.AddWithValue("@intIdFormaPago", idFormaPago);
            comando.Parameters.AddWithValue("@intClienteTipoDocumentoSunat", cliente.IdTipoDocumentoSunat);
            comando.Parameters.AddWithValue("@vchClienteNumeroDocumento", cliente.NumeroDocumento);
            comando.Parameters.AddWithValue("@vchClienteNombre", (object?)cliente.Nombre ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchClienteCorreo", (object?)cliente.Correo ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchClienteDireccion", (object?)cliente.Direccion ?? DBNull.Value);
            comando.Parameters.AddWithValue("@intClientePaisCodigo", cliente.PaisCodigo);
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

            var tvpCamposExtra = comando.Parameters.Add("@tvpCamposExtra", SqlDbType.Structured);
            tvpCamposExtra.TypeName = "dbo.TVP_CAMPO_EXTRA_DOCUMENTO_ELECTRONICO";
            tvpCamposExtra.Value = ConstruirTablaCamposExtra(camposExtra);

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
                IdExterno = lector.GetString(lector.GetOrdinal("IdExterno")),
                NumeroReferencia = LeerNullableString(lector, "NumeroReferencia"),
                TipoDocumentoCodigo = lector.GetString(lector.GetOrdinal("TipoDocumentoCodigo")),
                Serie = lector.GetString(lector.GetOrdinal("Serie")),
                Correlativo = lector.GetInt32(lector.GetOrdinal("Correlativo")),
                EstadoCodigo = lector.GetString(lector.GetOrdinal("EstadoCodigo")),
                FechaEmision = DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("FechaEmision"))),
                HoraEmision = TimeOnly.FromTimeSpan(lector.GetTimeSpan(lector.GetOrdinal("HoraEmision"))),
                MonedaCodigo = lector.GetString(lector.GetOrdinal("MonedaCodigo")),
                TipoCambio = lector.IsDBNull(lector.GetOrdinal("TipoCambio")) ? null : lector.GetDecimal(lector.GetOrdinal("TipoCambio")),
                TipoOperacionCodigo = lector.GetString(lector.GetOrdinal("TipoOperacionCodigo")),
                FormaPagoCodigo = lector.GetString(lector.GetOrdinal("FormaPagoCodigo")),
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
                TotalExportacion = lector.GetDecimal(lector.GetOrdinal("TotalExportacion")),
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
                    lector.GetString(lector.GetOrdinal("TributoSunatCodigo")),
                    lector.GetString(lector.GetOrdinal("TributoNombre")),
                    lector.GetString(lector.GetOrdinal("TributoTaxTypeCode")),
                    lector.GetString(lector.GetOrdinal("TributoCategoria")),
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
                cuotas.Add(LeerCuota(lector));
            }

            // Result set 6: campos extra
            await lector.NextResultAsync(cancellationToken);
            var camposExtra = new List<CampoExtraEntrada>();
            while (await lector.ReadAsync(cancellationToken))
            {
                camposExtra.Add(new CampoExtraEntrada(
                    lector.GetString(lector.GetOrdinal("Texto"))));
            }

            var detalle = new DocumentoElectronicoDetalle(cabecera, lineas, referencia, cuotas, camposExtra);
            return ResultadoOperacion<DocumentoElectronicoDetalle>.DeExito(mensaje, detalle);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<DocumentoElectronicoDetalle>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<string>> ObtenerTokenPublicoAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_DocumentoElectronico_ObtenerTokenPublico", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", idDocumentoElectronico);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<string>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var tokenPublico = lector.GetString(lector.GetOrdinal("TokenPublico"));

            return ResultadoOperacion<string>.DeExito(mensaje, tokenPublico);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<string>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<DocumentoElectronicoDetallePublico>> ObtenerPorTokenAsync(
        string tokenPublico, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_DocumentoElectronico_ObtenerPorToken", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchTokenPublico", tokenPublico);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<DocumentoElectronicoDetallePublico>(idTipoMensaje, mensaje, default);
            }

            // Result set 2: cabecera
            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var cabecera = new DocumentoElectronicoPublico(
                LeerNullableString(lector, "NumeroReferencia"),
                lector.GetString(lector.GetOrdinal("TipoDocumentoCodigo")),
                lector.GetString(lector.GetOrdinal("Serie")),
                lector.GetInt32(lector.GetOrdinal("Correlativo")),
                lector.GetString(lector.GetOrdinal("EstadoCodigo")),
                DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("FechaEmision"))),
                TimeOnly.FromTimeSpan(lector.GetTimeSpan(lector.GetOrdinal("HoraEmision"))),
                lector.GetString(lector.GetOrdinal("MonedaCodigo")),
                lector.IsDBNull(lector.GetOrdinal("TipoCambio")) ? null : lector.GetDecimal(lector.GetOrdinal("TipoCambio")),
                lector.GetString(lector.GetOrdinal("TipoOperacionCodigo")),
                lector.GetString(lector.GetOrdinal("FormaPagoCodigo")),
                lector.GetString(lector.GetOrdinal("EmpresaRuc")),
                lector.GetString(lector.GetOrdinal("EmpresaRazonSocial")),
                LeerNullableString(lector, "EmpresaNombreComercial"),
                lector.GetString(lector.GetOrdinal("EmpresaDireccion")),
                lector.GetString(lector.GetOrdinal("EmpresaUbigeo")),
                lector.GetString(lector.GetOrdinal("ClienteTipoDocumentoCodigo")),
                lector.GetString(lector.GetOrdinal("ClienteNumeroDocumento")),
                lector.GetString(lector.GetOrdinal("ClienteNombre")),
                LeerNullableString(lector, "ClienteDireccion"),
                LeerNullableString(lector, "ClienteCorreo"),
                lector.GetString(lector.GetOrdinal("ClientePaisCodigo")),
                lector.GetDecimal(lector.GetOrdinal("TotalGravado")),
                lector.GetDecimal(lector.GetOrdinal("TotalInafecto")),
                lector.GetDecimal(lector.GetOrdinal("TotalExonerado")),
                lector.GetDecimal(lector.GetOrdinal("TotalExportacion")),
                lector.GetDecimal(lector.GetOrdinal("TotalIgv")),
                lector.GetDecimal(lector.GetOrdinal("TotalIsc")),
                lector.GetDecimal(lector.GetOrdinal("TotalOtrosTributos")),
                lector.GetDecimal(lector.GetOrdinal("TotalDescuento")),
                lector.GetDecimal(lector.GetOrdinal("TotalCargo")),
                lector.GetDecimal(lector.GetOrdinal("TotalImporte")),
                LeerNullableString(lector, "SunatHash"),
                LeerNullableString(lector, "SunatCodigoRespuesta"),
                LeerNullableString(lector, "SunatDescripcionRespuesta"),
                LeerNullableDateTime(lector, "FechaAceptacion"),
                LeerNullableDateTime(lector, "FechaRechazo"),
                LeerNullableDateTime(lector, "FechaAnulacion"),
                lector.GetDateTime(lector.GetOrdinal("FchCre")));

            // Result set 3: líneas
            await lector.NextResultAsync(cancellationToken);
            var lineas = new List<LineaDocumentoElectronicoPublica>();
            while (await lector.ReadAsync(cancellationToken))
            {
                lineas.Add(new LineaDocumentoElectronicoPublica(
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
                    lector.GetString(lector.GetOrdinal("TributoSunatCodigo")),
                    lector.GetString(lector.GetOrdinal("TributoNombre")),
                    lector.GetString(lector.GetOrdinal("TributoTaxTypeCode")),
                    lector.GetString(lector.GetOrdinal("TributoCategoria")),
                    lector.GetDecimal(lector.GetOrdinal("PorcentajeIgv")),
                    lector.GetDecimal(lector.GetOrdinal("MontoIgv")),
                    lector.GetDecimal(lector.GetOrdinal("MontoIsc")),
                    lector.GetDecimal(lector.GetOrdinal("MontoOtrosTributos")),
                    lector.GetDecimal(lector.GetOrdinal("ValorLinea")),
                    lector.GetDecimal(lector.GetOrdinal("TotalLinea"))));
            }

            // Result set 4: referencia (0 o 1 fila — solo notas de crédito/débito)
            await lector.NextResultAsync(cancellationToken);
            ReferenciaDocumentoElectronicaPublica? referencia = null;
            if (await lector.ReadAsync(cancellationToken))
            {
                referencia = new ReferenciaDocumentoElectronicaPublica(
                    lector.GetString(lector.GetOrdinal("TipoDocumentoRelacionadoCodigo")),
                    lector.GetString(lector.GetOrdinal("SerieRelacionada")),
                    lector.GetInt32(lector.GetOrdinal("CorrelativoRelacionado")),
                    lector.GetString(lector.GetOrdinal("TipoReferenciaCodigo")),
                    lector.GetString(lector.GetOrdinal("MotivoCodigo")),
                    lector.GetString(lector.GetOrdinal("MotivoDescripcion")));
            }

            // Result set 5: cuotas (0 filas si fue Contado)
            await lector.NextResultAsync(cancellationToken);
            var cuotas = new List<CuotaDocumentoElectronicaPublica>();
            while (await lector.ReadAsync(cancellationToken))
            {
                cuotas.Add(new CuotaDocumentoElectronicaPublica(
                    lector.GetInt32(lector.GetOrdinal("NumeroCuota")),
                    DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("FechaVencimiento"))),
                    lector.GetDecimal(lector.GetOrdinal("Monto")),
                    lector.GetString(lector.GetOrdinal("EstadoCuotaCodigo")),
                    LeerNullableDateTime(lector, "FechaPago")));
            }

            var detalle = new DocumentoElectronicoDetallePublico(cabecera, lineas, referencia, cuotas);
            return ResultadoOperacion<DocumentoElectronicoDetallePublico>.DeExito(mensaje, detalle);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<DocumentoElectronicoDetallePublico>.DeErrorSistema(ex.Message);
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

    public async Task<ResultadoOperacion<ResultadoPaginado<FacturaResumenPedidoFactura>>> ListarParaPedidoFacturaAsync(
        int idInquilino, int idEmpresa, string? estadoCodigo, int? idFormaPago, DateOnly? fechaDesde, DateOnly? fechaHasta,
        string? busqueda, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_DocumentoElectronico_ListarParaPedidoFactura", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@vchEstadoCodigo", (object?)estadoCodigo ?? DBNull.Value);
            comando.Parameters.AddWithValue("@intIdFormaPago", (object?)idFormaPago ?? DBNull.Value);
            comando.Parameters.AddWithValue("@dtFechaDesde", (object?)fechaDesde?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
            comando.Parameters.AddWithValue("@dtFechaHasta", (object?)fechaHasta?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchBusqueda", (object?)busqueda ?? DBNull.Value);
            comando.Parameters.AddWithValue("@numPag", numeroPagina);
            comando.Parameters.AddWithValue("@intTamPag", tamanoPagina);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<ResultadoPaginado<FacturaResumenPedidoFactura>>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var totalRegistros = lector.GetInt32(lector.GetOrdinal("TotalRegistros"));
            var totalPaginas = lector.GetInt32(lector.GetOrdinal("TotalPaginas"));

            await lector.NextResultAsync(cancellationToken);
            var items = new List<FacturaResumenPedidoFactura>();
            while (await lector.ReadAsync(cancellationToken))
            {
                items.Add(new FacturaResumenPedidoFactura(
                    lector.GetInt32(lector.GetOrdinal("IdDocumentoElectronico")),
                    lector.GetString(lector.GetOrdinal("NumeroFactura")),
                    lector.GetString(lector.GetOrdinal("ClienteNombre")),
                    DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("FechaEmision"))),
                    lector.GetString(lector.GetOrdinal("FormaPagoCodigo")),
                    lector.GetDecimal(lector.GetOrdinal("TotalImporte")),
                    lector.GetString(lector.GetOrdinal("MonedaIcono")),
                    lector.GetString(lector.GetOrdinal("EstadoCodigo")),
                    lector.GetString(lector.GetOrdinal("ColorLetra")),
                    lector.GetString(lector.GetOrdinal("ColorFondo"))));
            }

            var paginado = new ResultadoPaginado<FacturaResumenPedidoFactura>(totalRegistros, totalPaginas, items);
            return ResultadoOperacion<ResultadoPaginado<FacturaResumenPedidoFactura>>.DeExito(mensaje, paginado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<ResultadoPaginado<FacturaResumenPedidoFactura>>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<IReadOnlyList<DocumentoSireRvie>>> ListarParaSireRvieAsync(
        int idInquilino, int idEmpresa, DateOnly periodo, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_DocumentoElectronico_ListarParaSireRvie", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@dtPeriodo", periodo.ToDateTime(TimeOnly.MinValue));

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<IReadOnlyList<DocumentoSireRvie>>(idTipoMensaje, mensaje, default);
            }

            var documentos = new List<DocumentoSireRvie>();
            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                documentos.Add(new DocumentoSireRvie(
                    lector.GetInt32(lector.GetOrdinal("IdDocumentoElectronico")),
                    lector.GetString(lector.GetOrdinal("EmpresaRuc")),
                    lector.GetString(lector.GetOrdinal("EmpresaRazonSocial")),
                    DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("FechaEmision"))),
                    lector.GetString(lector.GetOrdinal("TipoDocumentoCodigo")),
                    lector.GetString(lector.GetOrdinal("Serie")),
                    lector.GetInt32(lector.GetOrdinal("Correlativo")),
                    lector.GetString(lector.GetOrdinal("ClienteTipoDocumentoCodigo")),
                    lector.GetString(lector.GetOrdinal("ClienteNumeroDocumento")),
                    lector.GetString(lector.GetOrdinal("ClienteNombre")),
                    lector.GetDecimal(lector.GetOrdinal("TotalExportacion")),
                    lector.GetDecimal(lector.GetOrdinal("TotalGravado")),
                    lector.GetDecimal(lector.GetOrdinal("TotalIgv")),
                    lector.GetDecimal(lector.GetOrdinal("TotalExonerado")),
                    lector.GetDecimal(lector.GetOrdinal("TotalInafecto")),
                    lector.GetDecimal(lector.GetOrdinal("TotalIsc")),
                    lector.GetDecimal(lector.GetOrdinal("TotalOtrosTributos")),
                    lector.GetDecimal(lector.GetOrdinal("TotalImporte")),
                    lector.GetString(lector.GetOrdinal("MonedaCodigo")),
                    LeerNullableDecimal(lector, "TipoCambio"),
                    LeerNullableDateOnly(lector, "FechaEmisionDocModificado"),
                    LeerNullableString(lector, "TipoDocumentoRelacionadoCodigo"),
                    LeerNullableString(lector, "SerieRelacionada"),
                    LeerNullableInt(lector, "CorrelativoRelacionado")));
            }

            return ResultadoOperacion<IReadOnlyList<DocumentoSireRvie>>.DeExito(mensaje, documentos);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<IReadOnlyList<DocumentoSireRvie>>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<EstadoDocumentoElectronicoActualizado>> ActualizarEstadoSunatAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, EstadoMaestroCodigo estadoCodigo, string? sunatHash,
        string? sunatCodigoRespuesta, string? sunatDescripcionRespuesta, string? sunatTicket, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_DocumentoElectronico_ActualizarEstadoSunat", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", idDocumentoElectronico);
            comando.Parameters.AddWithValue("@intEstadoCodigo", (int)estadoCodigo);
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

    public async Task<ResultadoOperacion<DocumentoElectronicoCambiosGuardados>> GuardarCambiosAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, int idFormaPago, string? numeroReferencia,
        int idMonedaMaestro, decimal? tipoCambio, int idTipoOperacionMaestro,
        IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas, IReadOnlyList<CuotaDocumentoElectronico> cuotas,
        IReadOnlyList<CampoExtraEntrada> camposExtra, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_DocumentoElectronico_GuardarCambios", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", idDocumentoElectronico);
            comando.Parameters.AddWithValue("@intIdFormaPago", idFormaPago);
            comando.Parameters.AddWithValue("@vchNumeroReferencia", (object?)numeroReferencia ?? DBNull.Value);
            comando.Parameters.AddWithValue("@intIdMonedaMaestro", idMonedaMaestro);
            comando.Parameters.AddWithValue("@decTipoCambio", (object?)tipoCambio ?? DBNull.Value);
            comando.Parameters.AddWithValue("@intIdTipoOperacionMaestro", idTipoOperacionMaestro);

            var tvpLineas = comando.Parameters.Add("@tvpLineas", SqlDbType.Structured);
            tvpLineas.TypeName = "dbo.TVP_LINEA_DOCUMENTO_ELECTRONICO_EDICION";
            tvpLineas.Value = ConstruirTablaLineasEdicion(lineas);

            var tvpCuotas = comando.Parameters.Add("@tvpCuotas", SqlDbType.Structured);
            tvpCuotas.TypeName = "dbo.TVP_CUOTA_DOCUMENTO_ELECTRONICO_EDICION";
            tvpCuotas.Value = ConstruirTablaCuotasEdicion(cuotas);

            var tvpCamposExtra = comando.Parameters.Add("@tvpCamposExtra", SqlDbType.Structured);
            tvpCamposExtra.TypeName = "dbo.TVP_CAMPO_EXTRA_DOCUMENTO_ELECTRONICO_EDICION";
            tvpCamposExtra.Value = ConstruirTablaCamposExtraEdicion(camposExtra);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<DocumentoElectronicoCambiosGuardados>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            var lineasFinales = new List<LineaDocumentoElectronico>();
            while (await lector.ReadAsync(cancellationToken))
            {
                lineasFinales.Add(LeerLinea(lector));
            }

            await lector.NextResultAsync(cancellationToken);
            var cuotasFinales = new List<CuotaDocumentoElectronico>();
            while (await lector.ReadAsync(cancellationToken))
            {
                cuotasFinales.Add(LeerCuota(lector));
            }

            return ResultadoOperacion<DocumentoElectronicoCambiosGuardados>.DeExito(
                mensaje, new DocumentoElectronicoCambiosGuardados(lineasFinales, cuotasFinales));
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<DocumentoElectronicoCambiosGuardados>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<CuotaDocumentoElectronico>> ActualizarEstadoCuotaAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, int idCuotaDocumentoElectronico,
        EstadoCuotaCodigo estadoCuotaCodigo, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_CuotaDocumentoElectronico_ActualizarEstado", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", idDocumentoElectronico);
            comando.Parameters.AddWithValue("@intIdCuotaDocumentoElectronico", idCuotaDocumentoElectronico);
            comando.Parameters.AddWithValue("@intEstadoCuotaCodigo", (int)estadoCuotaCodigo);

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

    public async Task<ResultadoOperacion<IReadOnlyList<EventoDocumentoReciente>>> ListarEventosRecientesAsync(
        int idInquilino, int ultimoIdEvento, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_DocumentoElectronico_ListarEventosRecientes", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intUltimoIdEvento", ultimoIdEvento);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<IReadOnlyList<EventoDocumentoReciente>>(idTipoMensaje, mensaje, default);
            }

            var eventos = new List<EventoDocumentoReciente>();
            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                eventos.Add(new EventoDocumentoReciente(
                    lector.GetInt32(lector.GetOrdinal("IdEventoDocumento")),
                    lector.GetInt32(lector.GetOrdinal("IdDocumentoElectronico")),
                    lector.GetInt32(lector.GetOrdinal("IdEstadoNuevoMaestro")),
                    lector.GetString(lector.GetOrdinal("EstadoCodigo")),
                    lector.GetInt32(lector.GetOrdinal("EsAnulacion")) == 1));
            }

            return ResultadoOperacion<IReadOnlyList<EventoDocumentoReciente>>.DeExito(mensaje, eventos);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<IReadOnlyList<EventoDocumentoReciente>>.DeErrorSistema(ex.Message);
        }
    }

    /// El orden de columnas debe coincidir exactamente con TVP_LINEA_DOCUMENTO_ELECTRONICO_EDICION.
    private static DataTable ConstruirTablaLineasEdicion(IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("IdLineaDocumentoElectronico", typeof(int));
        tabla.Columns.Add("NumeroLinea", typeof(int));
        tabla.Columns.Add("ProductoCodigo", typeof(string));
        tabla.Columns.Add("ProductoSunatCodigo", typeof(string));
        tabla.Columns.Add("Descripcion", typeof(string));
        tabla.Columns.Add("IdUnidadMedidaMaestro", typeof(int));
        tabla.Columns.Add("Cantidad", typeof(decimal));
        tabla.Columns.Add("ValorUnitario", typeof(decimal));
        tabla.Columns.Add("MontoDescuento", typeof(decimal));
        tabla.Columns.Add("IdAfectacionIgvMaestro", typeof(int));
        tabla.Columns.Add("PorcentajeIgv", typeof(decimal));

        foreach (var linea in lineas)
        {
            tabla.Rows.Add(
                linea.IdLineaDocumentoElectronico, linea.NumeroLinea, linea.ProductoCodigo, (object?)linea.ProductoSunatCodigo ?? DBNull.Value,
                linea.Descripcion, linea.IdUnidadMedidaMaestro, linea.Cantidad, linea.ValorUnitario,
                linea.MontoDescuento, linea.IdAfectacionIgvMaestro, linea.PorcentajeIgv);
        }

        return tabla;
    }

    /// El orden de columnas debe coincidir exactamente con TVP_CUOTA_DOCUMENTO_ELECTRONICO_EDICION.
    private static DataTable ConstruirTablaCuotasEdicion(IReadOnlyList<CuotaDocumentoElectronico> cuotas)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("IdCuotaDocumentoElectronico", typeof(int));
        tabla.Columns.Add("NumeroCuota", typeof(int));
        tabla.Columns.Add("FechaVencimiento", typeof(DateTime));
        tabla.Columns.Add("Monto", typeof(decimal));

        foreach (var cuota in cuotas)
        {
            tabla.Rows.Add(cuota.IdCuotaDocumentoElectronico, cuota.NumeroCuota, cuota.FechaVencimiento.ToDateTime(TimeOnly.MinValue), cuota.Monto);
        }

        return tabla;
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
        lector.GetString(lector.GetOrdinal("TributoSunatCodigo")),
        lector.GetString(lector.GetOrdinal("TributoNombre")),
        lector.GetString(lector.GetOrdinal("TributoTaxTypeCode")),
        lector.GetString(lector.GetOrdinal("TributoCategoria")),
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
        lector.GetInt32(lector.GetOrdinal("IdCuotaDocumentoElectronico")),
        lector.GetString(lector.GetOrdinal("EstadoCuotaCodigo")),
        lector.IsDBNull(lector.GetOrdinal("FechaPago")) ? null : lector.GetDateTime(lector.GetOrdinal("FechaPago")));

    /// El orden de columnas debe coincidir exactamente con TVP_LINEA_DOCUMENTO_ELECTRONICO (02_CrearTipos_MsFacturacion.sql) —
    /// una TVP basada en DataTable se mapea posicionalmente, no por nombre.
    private static DataTable ConstruirTablaLineas(IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("NumeroLinea", typeof(int));
        tabla.Columns.Add("ProductoCodigo", typeof(string));
        tabla.Columns.Add("ProductoSunatCodigo", typeof(string));
        tabla.Columns.Add("Descripcion", typeof(string));
        tabla.Columns.Add("IdUnidadMedidaMaestro", typeof(int));
        tabla.Columns.Add("Cantidad", typeof(decimal));
        tabla.Columns.Add("ValorUnitario", typeof(decimal));
        tabla.Columns.Add("MontoDescuento", typeof(decimal));
        tabla.Columns.Add("IdAfectacionIgvMaestro", typeof(int));
        tabla.Columns.Add("PorcentajeIgv", typeof(decimal));

        foreach (var linea in lineas)
        {
            tabla.Rows.Add(
                linea.NumeroLinea, linea.ProductoCodigo, (object?)linea.ProductoSunatCodigo ?? DBNull.Value,
                linea.Descripcion, linea.IdUnidadMedidaMaestro, linea.Cantidad, linea.ValorUnitario,
                linea.MontoDescuento, linea.IdAfectacionIgvMaestro, linea.PorcentajeIgv);
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

    /// El orden de columnas debe coincidir exactamente con TVP_CAMPO_EXTRA_DOCUMENTO_ELECTRONICO
    /// (02_CrearTipos_MsFacturacion.sql) — una TVP basada en DataTable se mapea posicionalmente, no por nombre.
    private static DataTable ConstruirTablaCamposExtra(IReadOnlyList<CampoExtraEntrada> camposExtra)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("Texto", typeof(string));

        foreach (var campo in camposExtra)
        {
            tabla.Rows.Add(campo.Texto);
        }

        return tabla;
    }

    /// El orden de columnas debe coincidir exactamente con TVP_CAMPO_EXTRA_DOCUMENTO_ELECTRONICO_EDICION.
    private static DataTable ConstruirTablaCamposExtraEdicion(IReadOnlyList<CampoExtraEntrada> camposExtra)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("IdCampoExtraDocumentoElectronico", typeof(int));
        tabla.Columns.Add("Texto", typeof(string));

        foreach (var campo in camposExtra)
        {
            tabla.Rows.Add(campo.IdCampoExtraDocumentoElectronico, campo.Texto);
        }

        return tabla;
    }

    private static string? LeerNullableString(SqlDataReader lector, string columna)
    {
        var ordinal = lector.GetOrdinal(columna);
        return lector.IsDBNull(ordinal) ? null : lector.GetString(ordinal);
    }

    private static decimal? LeerNullableDecimal(SqlDataReader lector, string columna)
    {
        var ordinal = lector.GetOrdinal(columna);
        return lector.IsDBNull(ordinal) ? null : lector.GetDecimal(ordinal);
    }

    private static int? LeerNullableInt(SqlDataReader lector, string columna)
    {
        var ordinal = lector.GetOrdinal(columna);
        return lector.IsDBNull(ordinal) ? null : lector.GetInt32(ordinal);
    }

    private static DateOnly? LeerNullableDateOnly(SqlDataReader lector, string columna)
    {
        var ordinal = lector.GetOrdinal(columna);
        return lector.IsDBNull(ordinal) ? null : DateOnly.FromDateTime(lector.GetDateTime(ordinal));
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

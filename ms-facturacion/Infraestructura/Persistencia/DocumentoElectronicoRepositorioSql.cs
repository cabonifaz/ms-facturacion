using MySqlConnector;
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
        int idMonedaMaestro, decimal? tipoCambio, int idTipoOperacionMaestro, int? idFormaPago, ClienteDatosEntrada cliente,
        DocumentoAfectadoEntrada? documentoAfectado, IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas,
        IReadOnlyList<CuotaDocumentoElectronicoEntrada> cuotas, IReadOnlyList<CampoExtraEntrada> camposExtra,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_vchIdExterno", idExterno);
            comando.Parameters.AddWithValue("@p_vchNumeroReferencia", (object?)numeroReferencia ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_intIdTipoDocumentoMaestro", idTipoDocumentoMaestro);
            comando.Parameters.AddWithValue("@p_dtFechaEmision", fechaEmision.ToDateTime(TimeOnly.MinValue));
            comando.Parameters.Add("@p_tmHoraEmision", MySqlDbType.Time).Value = horaEmision.ToTimeSpan();
            comando.Parameters.AddWithValue("@p_intIdMonedaMaestro", idMonedaMaestro);
            comando.Parameters.AddWithValue("@p_decTipoCambio", (object?)tipoCambio ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_intIdTipoOperacionMaestro", idTipoOperacionMaestro);
            comando.Parameters.AddWithValue("@p_intIdFormaPago", (object?)idFormaPago ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_intClienteTipoDocumentoSunat", cliente.IdTipoDocumentoSunat);
            comando.Parameters.AddWithValue("@p_vchClienteNumeroDocumento", cliente.NumeroDocumento);
            comando.Parameters.AddWithValue("@p_vchClienteNombre", (object?)cliente.Nombre ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchClienteCorreo", (object?)cliente.Correo ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchClienteDireccion", (object?)cliente.Direccion ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_intClientePaisCodigo", cliente.PaisCodigo);
            comando.Parameters.AddWithValue("@p_intIdDocumentoElectronicoRelacionado", (object?)documentoAfectado?.IdDocumentoElectronicoRelacionado ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_intIdMotivoMaestro", (object?)documentoAfectado?.IdMotivoMaestro ?? DBNull.Value);

            comando.Parameters.AddWithValue("@p_jsonLineas", ConstruirJsonLineas(lineas));
            comando.Parameters.AddWithValue("@p_jsonCuotas", ConstruirJsonCuotas(cuotas));
            comando.Parameters.AddWithValue("@p_jsonCamposExtra", ConstruirJsonCamposExtra(camposExtra));

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
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_Obtener", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdDocumentoElectronico", idDocumentoElectronico);

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
                FormaPagoCodigo = LeerNullableString(lector, "FormaPagoCodigo"),
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
                    LeerNullableInt(lector, "IdPedidoFacturaLinea"),
                    LeerNullableString(lector, "ProductoCodigo"),
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
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_ObtenerTokenPublico", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdDocumentoElectronico", idDocumentoElectronico);

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

    public async Task<ResultadoOperacion<DatosParaNota>> ObtenerParaNotaAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_ObtenerParaNota", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdDocumentoElectronico", idDocumentoElectronico);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<DatosParaNota>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var cliente = new ClienteDatosEntrada(
                lector.GetInt32(lector.GetOrdinal("IdTipoDocumentoSunat")),
                lector.GetString(lector.GetOrdinal("NumeroDocumento")),
                LeerNullableString(lector, "Nombre"),
                LeerNullableString(lector, "Correo"),
                LeerNullableString(lector, "Direccion"),
                lector.GetInt32(lector.GetOrdinal("PaisCodigo")));
            var idMonedaMaestro = lector.GetInt32(lector.GetOrdinal("IdMonedaMaestro"));
            var tipoCambio = LeerNullableDecimal(lector, "TipoCambio");

            await lector.NextResultAsync(cancellationToken);
            var productos = new List<ProductoDocumentoResumen>();
            while (await lector.ReadAsync(cancellationToken))
            {
                productos.Add(new ProductoDocumentoResumen(
                    lector.GetInt32(lector.GetOrdinal("NumeroLinea")),
                    LeerNullableString(lector, "ProductoCodigo")));
            }

            return ResultadoOperacion<DatosParaNota>.DeExito(mensaje, new DatosParaNota(cliente, idMonedaMaestro, tipoCambio, productos));
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<DatosParaNota>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<DocumentoElectronicoDetallePublico>> ObtenerPorTokenAsync(
        string tokenPublico, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_ObtenerPorToken", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchTokenPublico", tokenPublico);

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
                LeerNullableString(lector, "FormaPagoCodigo"),
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
                    LeerNullableString(lector, "ProductoCodigo"),
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

    public async Task<ResultadoOperacion<IdentificadorDocumentoPorToken>> ObtenerIdPorTokenAsync(
        string tokenPublico, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_ObtenerIdPorToken", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchTokenPublico", tokenPublico);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<IdentificadorDocumentoPorToken>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var identificador = new IdentificadorDocumentoPorToken(
                lector.GetInt32(lector.GetOrdinal("IdDocumentoElectronico")),
                lector.GetInt32(lector.GetOrdinal("IdInquilino")));

            return ResultadoOperacion<IdentificadorDocumentoPorToken>.DeExito(mensaje, identificador);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<IdentificadorDocumentoPorToken>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<ResultadoPaginado<DocumentoElectronicoResumen>>> ListarAsync(
        int idInquilino, int idEmpresa, string? estadoCodigo, string? busqueda, DateOnly? fechaDesde, DateOnly? fechaHasta,
        int numeroPagina, int tamanoPagina, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_Listar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_vchEstadoCodigo", (object?)estadoCodigo ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchBusqueda", (object?)busqueda ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_dtFechaDesde", (object?)fechaDesde?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_dtFechaHasta", (object?)fechaHasta?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_numPag", numeroPagina);
            comando.Parameters.AddWithValue("@p_intTamPag", tamanoPagina);

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
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_ListarParaPedidoFactura", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_vchEstadoCodigo", (object?)estadoCodigo ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_intIdFormaPago", (object?)idFormaPago ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_dtFechaDesde", (object?)fechaDesde?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_dtFechaHasta", (object?)fechaHasta?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchBusqueda", (object?)busqueda ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_numPag", numeroPagina);
            comando.Parameters.AddWithValue("@p_intTamPag", tamanoPagina);

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
                    lector.GetString(lector.GetOrdinal("TipoDocumentoTexto")),
                    LeerNullableString(lector, "DocumentoAfectado"),
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
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_ListarParaSireRvie", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_dtPeriodo", periodo.ToDateTime(TimeOnly.MinValue));

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
                    LeerNullableString(lector, "EstadoAnulacionCodigo"),
                    LeerNullableDateOnly(lector, "FechaEmisionDocModificado"),
                    LeerNullableString(lector, "TipoDocumentoRelacionadoCodigo"),
                    LeerNullableString(lector, "SerieRelacionada"),
                    LeerNullableInt(lector, "CorrelativoRelacionado"),
                    lector.GetBoolean(lector.GetOrdinal("TieneLineaIvap"))));
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
        string? sunatCodigoRespuesta, string? sunatDescripcionRespuesta, string? sunatTicket, DateTime fecha, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_ActualizarEstadoSunat", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdDocumentoElectronico", idDocumentoElectronico);
            comando.Parameters.AddWithValue("@p_intEstadoCodigo", (int)estadoCodigo);
            comando.Parameters.AddWithValue("@p_vchSunatHash", (object?)sunatHash ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchSunatCodigoRespuesta", (object?)sunatCodigoRespuesta ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchSunatDescripcionRespuesta", (object?)sunatDescripcionRespuesta ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchSunatTicket", (object?)sunatTicket ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_dtmFecha", fecha);

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

    public async Task<ResultadoOperacion<IReadOnlyList<EstadoDocumentoElectronicoActualizado>>> AnularManualmenteAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, string motivo, DateTime fechaAnulacion,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_AnularManualmente", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdDocumentoElectronico", idDocumentoElectronico);
            comando.Parameters.AddWithValue("@p_vchMotivo", motivo);
            comando.Parameters.AddWithValue("@p_dtmFechaAnulacion", fechaAnulacion);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<IReadOnlyList<EstadoDocumentoElectronicoActualizado>>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);

            var afectados = new List<EstadoDocumentoElectronicoActualizado>();
            while (await lector.ReadAsync(cancellationToken))
            {
                afectados.Add(new EstadoDocumentoElectronicoActualizado(
                    lector.GetInt32(lector.GetOrdinal("IdDocumentoElectronico")),
                    lector.GetString(lector.GetOrdinal("EstadoCodigo"))));
            }

            return ResultadoOperacion<IReadOnlyList<EstadoDocumentoElectronicoActualizado>>.DeExito(mensaje, afectados);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<IReadOnlyList<EstadoDocumentoElectronicoActualizado>>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<IReadOnlyList<DocumentoAnulacionManualPreview>>> PrevisualizarAnulacionManualAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_PrevisualizarAnulacionManual", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdDocumentoElectronico", idDocumentoElectronico);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<IReadOnlyList<DocumentoAnulacionManualPreview>>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);

            var afectados = new List<DocumentoAnulacionManualPreview>();
            while (await lector.ReadAsync(cancellationToken))
            {
                afectados.Add(new DocumentoAnulacionManualPreview(
                    lector.GetInt32(lector.GetOrdinal("IdDocumentoElectronico")),
                    lector.GetString(lector.GetOrdinal("TipoDocumentoCodigo")),
                    lector.GetString(lector.GetOrdinal("NumeroDocumento")),
                    DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("FechaEmision"))),
                    lector.GetString(lector.GetOrdinal("EstadoCodigo"))));
            }

            return ResultadoOperacion<IReadOnlyList<DocumentoAnulacionManualPreview>>.DeExito(mensaje, afectados);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<IReadOnlyList<DocumentoAnulacionManualPreview>>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<bool>> ActualizarFechaEmisionAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico,
        DateOnly fechaEmision, TimeOnly horaEmision, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_ActualizarFechaEmision", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdDocumentoElectronico", idDocumentoElectronico);
            comando.Parameters.AddWithValue("@p_dtmFechaEmision", fechaEmision.ToDateTime(TimeOnly.MinValue));
            comando.Parameters.Add("@p_timHoraEmision", MySqlDbType.Time).Value = horaEmision.ToTimeSpan();

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

    public async Task<ResultadoOperacion<bool>> EliminarBorradorAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_EliminarBorrador", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdDocumentoElectronico", idDocumentoElectronico);

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

    public async Task<ResultadoOperacion<bool>> ValidarSaldoNotaCreditoAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_ValidarSaldoNotaCredito", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdDocumentoElectronico", idDocumentoElectronico);

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
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, string idExterno, int? idFormaPago, string? numeroReferencia,
        int idMonedaMaestro, decimal? tipoCambio, int idTipoOperacionMaestro, int? idMotivoMaestro,
        IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas, IReadOnlyList<CuotaDocumentoElectronicoEntrada> cuotas,
        IReadOnlyList<CampoExtraEntrada> camposExtra, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_GuardarCambios", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdDocumentoElectronico", idDocumentoElectronico);
            comando.Parameters.AddWithValue("@p_vchIdExterno", idExterno);
            comando.Parameters.AddWithValue("@p_intIdFormaPago", (object?)idFormaPago ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchNumeroReferencia", (object?)numeroReferencia ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_intIdMonedaMaestro", idMonedaMaestro);
            comando.Parameters.AddWithValue("@p_decTipoCambio", (object?)tipoCambio ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_intIdTipoOperacionMaestro", idTipoOperacionMaestro);
            comando.Parameters.AddWithValue("@p_intIdMotivoMaestro", (object?)idMotivoMaestro ?? DBNull.Value);

            comando.Parameters.AddWithValue("@p_jsonLineas", ConstruirJsonLineasEdicion(lineas));
            comando.Parameters.AddWithValue("@p_jsonCuotas", ConstruirJsonCuotasEdicion(cuotas));
            comando.Parameters.AddWithValue("@p_jsonCamposExtra", ConstruirJsonCamposExtraEdicion(camposExtra));

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
        EstadoCuotaCodigo estadoCuotaCodigo, DateTime? fechaPago, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_CuotaDocumentoElectronico_ActualizarEstado", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdDocumentoElectronico", idDocumentoElectronico);
            comando.Parameters.AddWithValue("@p_intIdCuotaDocumentoElectronico", idCuotaDocumentoElectronico);
            comando.Parameters.AddWithValue("@p_intEstadoCuotaCodigo", (int)estadoCuotaCodigo);
            comando.Parameters.AddWithValue("@p_dtmFechaPago", (object?)fechaPago ?? DBNull.Value);

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
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_ListarEventosRecientes", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intUltimoIdEvento", ultimoIdEvento);

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

    public async Task<ResultadoOperacion<ResumenFacturacion>> ObtenerResumenFacturacionAsync(
        int idInquilino, int idEmpresa, DateOnly? fechaDesde, DateOnly? fechaHasta, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_DocumentoElectronico_ObtenerResumenFacturacion", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_dtFechaDesde", (object?)fechaDesde?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_dtFechaHasta", (object?)fechaHasta?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<ResumenFacturacion>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var resumen = new ResumenFacturacion(
                lector.GetInt32(lector.GetOrdinal("CantidadFacturas")),
                lector.GetDecimal(lector.GetOrdinal("MontoTotalPEN")),
                LeerNullableDecimal(lector, "PromedioIngresoPEN"),
                lector.GetString(lector.GetOrdinal("MonedaIcono")));

            return ResultadoOperacion<ResumenFacturacion>.DeExito(mensaje, resumen);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<ResumenFacturacion>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<MontosFacturacion>> ObtenerMontosFacturacionAsync(
        int idInquilino, int idEmpresa, DateOnly? fechaDesde, DateOnly? fechaHasta, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_Facturacion_ObtenerMontosFacturacion", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_dtFechaDesde", (object?)fechaDesde?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_dtFechaHasta", (object?)fechaHasta?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<MontosFacturacion>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var montos = new MontosFacturacion(
                lector.GetDecimal(lector.GetOrdinal("TotalFacturado")),
                lector.GetDecimal(lector.GetOrdinal("TotalNotasCredito")),
                lector.GetDecimal(lector.GetOrdinal("TotalNotasDebito")),
                lector.GetString(lector.GetOrdinal("MonedaIcono")));

            return ResultadoOperacion<MontosFacturacion>.DeExito(mensaje, montos);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<MontosFacturacion>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<IReadOnlyList<DesgloseEstadoFacturacion>>> ObtenerDesgloseEstadoFacturacionAsync(
        int idInquilino, int idEmpresa, DateOnly? fechaDesde, DateOnly? fechaHasta, int? idTipoDocumentoMaestro,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_Facturacion_ObtenerDesgloseEstado", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_dtFechaDesde", (object?)fechaDesde?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_dtFechaHasta", (object?)fechaHasta?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_intIdTipoDocumentoMaestro", (object?)idTipoDocumentoMaestro ?? DBNull.Value);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<IReadOnlyList<DesgloseEstadoFacturacion>>(idTipoMensaje, mensaje, default);
            }

            var desglose = new List<DesgloseEstadoFacturacion>();
            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                desglose.Add(new DesgloseEstadoFacturacion(
                    LeerNullableInt(lector, "IdEstadoMaestro"),
                    lector.GetString(lector.GetOrdinal("Estado")),
                    lector.GetInt32(lector.GetOrdinal("CantidadFacturas")),
                    lector.GetDecimal(lector.GetOrdinal("MontoFacturado"))));
            }

            return ResultadoOperacion<IReadOnlyList<DesgloseEstadoFacturacion>>.DeExito(mensaje, desglose);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<IReadOnlyList<DesgloseEstadoFacturacion>>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<IReadOnlyList<EvolucionFacturacion>>> ObtenerEvolucionFacturacionAsync(
        int idInquilino, int idEmpresa, DateOnly? fechaDesde, DateOnly? fechaHasta, int granularidad,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_Facturacion_ObtenerEvolucion", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_dtFechaDesde", (object?)fechaDesde?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_dtFechaHasta", (object?)fechaHasta?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_intGranularidad", granularidad);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<IReadOnlyList<EvolucionFacturacion>>(idTipoMensaje, mensaje, default);
            }

            var serie = new List<EvolucionFacturacion>();
            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                serie.Add(new EvolucionFacturacion(
                    lector.GetString(lector.GetOrdinal("Periodo")),
                    lector.GetString(lector.GetOrdinal("Etiqueta")),
                    lector.GetInt32(lector.GetOrdinal("CantidadPedidos")),
                    lector.GetDecimal(lector.GetOrdinal("MontoFacturado"))));
            }

            return ResultadoOperacion<IReadOnlyList<EvolucionFacturacion>>.DeExito(mensaje, serie);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<IReadOnlyList<EvolucionFacturacion>>.DeErrorSistema(ex.Message);
        }
    }

    /// Las propiedades del proyectado deben coincidir exactamente (nombre y forma) con las columnas leídas
    /// por JSON_TABLE(p_jsonLineas, ...) en SP_DocumentoElectronico_Insertar (facturacion-mysql).
    private static string ConstruirJsonLineas(IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas) =>
        System.Text.Json.JsonSerializer.Serialize(lineas.Select(linea => new
        {
            linea.NumeroLinea,
            ProductoCodigo = EscribirNullableString(linea.ProductoCodigo),
            linea.ProductoSunatCodigo,
            linea.Descripcion,
            linea.IdUnidadMedidaMaestro,
            linea.Cantidad,
            linea.ValorUnitario,
            linea.MontoDescuento,
            linea.IdAfectacionIgvMaestro,
            linea.PorcentajeIgv
        }));

    /// Debe coincidir exactamente con las columnas leídas por JSON_TABLE(p_jsonCuotas, ...) en
    /// SP_DocumentoElectronico_Insertar.
    private static string ConstruirJsonCuotas(IReadOnlyList<CuotaDocumentoElectronicoEntrada> cuotas) =>
        System.Text.Json.JsonSerializer.Serialize(cuotas.Select(cuota => new
        {
            cuota.NumeroCuota,
            cuota.FechaVencimiento,
            cuota.Monto,
            cuota.IdEstadoCuotaMaestro,
            cuota.FechaPago
        }));

    /// Debe coincidir exactamente con las columnas leídas por JSON_TABLE(p_jsonCamposExtra, ...) en
    /// SP_DocumentoElectronico_Insertar.
    private static string ConstruirJsonCamposExtra(IReadOnlyList<CampoExtraEntrada> camposExtra) =>
        System.Text.Json.JsonSerializer.Serialize(camposExtra.Select(campo => new { campo.Texto }));

    /// Debe coincidir exactamente con las columnas leídas por JSON_TABLE(p_jsonLineas, ...) en
    /// SP_DocumentoElectronico_GuardarCambios.
    private static string ConstruirJsonLineasEdicion(IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas) =>
        System.Text.Json.JsonSerializer.Serialize(lineas.Select(linea => new
        {
            linea.IdLineaDocumentoElectronico,
            linea.NumeroLinea,
            ProductoCodigo = EscribirNullableString(linea.ProductoCodigo),
            linea.ProductoSunatCodigo,
            linea.Descripcion,
            linea.IdUnidadMedidaMaestro,
            linea.Cantidad,
            linea.ValorUnitario,
            linea.MontoDescuento,
            linea.IdAfectacionIgvMaestro,
            linea.PorcentajeIgv
        }));

    /// Debe coincidir exactamente con las columnas leídas por JSON_TABLE(p_jsonCuotas, ...) en
    /// SP_DocumentoElectronico_GuardarCambios.
    private static string ConstruirJsonCuotasEdicion(IReadOnlyList<CuotaDocumentoElectronicoEntrada> cuotas) =>
        System.Text.Json.JsonSerializer.Serialize(cuotas.Select(cuota => new
        {
            cuota.IdCuotaDocumentoElectronico,
            cuota.NumeroCuota,
            cuota.FechaVencimiento,
            cuota.Monto,
            cuota.IdEstadoCuotaMaestro,
            cuota.FechaPago
        }));

    /// Debe coincidir exactamente con las columnas leídas por JSON_TABLE(p_jsonCamposExtra, ...) en
    /// SP_DocumentoElectronico_GuardarCambios.
    private static string ConstruirJsonCamposExtraEdicion(IReadOnlyList<CampoExtraEntrada> camposExtra) =>
        System.Text.Json.JsonSerializer.Serialize(camposExtra.Select(campo => new
        {
            campo.IdCampoExtraDocumentoElectronico,
            campo.Texto
        }));

    // IdPedidoFacturaLinea: SP_DocumentoElectronico_GuardarCambios no lo devuelve (a diferencia de
    // SP_DocumentoElectronico_Obtener) — el llamador ya lo mandó en la misma request que generó estas
    // líneas, no hace falta que el SP se lo confirme de vuelta.
    private static LineaDocumentoElectronico LeerLinea(MySqlDataReader lector) => new(
        lector.GetInt32(lector.GetOrdinal("IdLineaDocumentoElectronico")),
        lector.GetInt32(lector.GetOrdinal("NumeroLinea")),
        null,
        LeerNullableString(lector, "ProductoCodigo"),
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

    private static CuotaDocumentoElectronico LeerCuota(MySqlDataReader lector) => new(
        lector.GetInt32(lector.GetOrdinal("IdCuotaDocumentoElectronico")),
        lector.GetInt32(lector.GetOrdinal("NumeroCuota")),
        DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("FechaVencimiento"))),
        lector.GetDecimal(lector.GetOrdinal("Monto")),
        lector.GetString(lector.GetOrdinal("EstadoCuotaCodigo")),
        lector.IsDBNull(lector.GetOrdinal("FechaPago")) ? null : lector.GetDateTime(lector.GetOrdinal("FechaPago")));

    private static string? LeerNullableString(MySqlDataReader lector, string columna)
    {
        var ordinal = lector.GetOrdinal(columna);
        return lector.IsDBNull(ordinal) ? null : lector.GetString(ordinal);
    }

    /// Para columnas opcionales tipo string (p.ej. ProductoCodigo): "" se guarda como NULL, no como cadena
    /// vacía — evita dos representaciones distintas de "sin dato" en la misma columna.
    private static string? EscribirNullableString(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor;

    private static decimal? LeerNullableDecimal(MySqlDataReader lector, string columna)
    {
        var ordinal = lector.GetOrdinal(columna);
        return lector.IsDBNull(ordinal) ? null : lector.GetDecimal(ordinal);
    }

    private static int? LeerNullableInt(MySqlDataReader lector, string columna)
    {
        var ordinal = lector.GetOrdinal(columna);
        return lector.IsDBNull(ordinal) ? null : lector.GetInt32(ordinal);
    }

    private static DateOnly? LeerNullableDateOnly(MySqlDataReader lector, string columna)
    {
        var ordinal = lector.GetOrdinal(columna);
        return lector.IsDBNull(ordinal) ? null : DateOnly.FromDateTime(lector.GetDateTime(ordinal));
    }

    private static DateTime? LeerNullableDateTime(MySqlDataReader lector, string columna)
    {
        var ordinal = lector.GetOrdinal(columna);
        return lector.IsDBNull(ordinal) ? null : lector.GetDateTime(ordinal);
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

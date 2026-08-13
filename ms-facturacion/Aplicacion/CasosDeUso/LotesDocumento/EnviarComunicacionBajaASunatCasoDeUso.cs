using System.Security.Cryptography;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.LotesDocumento;

/// Crea el lote de Comunicación de Baja y lo envía a SUNAT en el mismo paso (sendSummary siempre
/// termina en un ticket, nunca en un resultado final — a diferencia del sendBill síncrono, aquí el
/// ticket ES el resultado esperable de éxito). Depende solo de Puertos, nunca de otros Casos de Uso.
public sealed class EnviarComunicacionBajaASunatCasoDeUso(
    ILoteDocumentoRepositorio loteRepositorio,
    IDocumentoElectronicoRepositorio documentoRepositorio,
    IEmpresaRepositorio empresaRepositorio,
    IConfiguracionFacturacionEmpresaRepositorio configuracionRepositorio,
    ICredencialInquilinoRepositorio credencialRepositorio,
    ICifradoInquilinoServicio cifradoServicio,
    IConstructorXmlBajaServicio constructorXml,
    IFirmadorXmlServicio firmador,
    IProveedorCertificadoServicio proveedorCertificado,
    IEmpaquetadorZipServicio empaquetador,
    IAlmacenamientoArchivosServicio almacenamiento,
    IArchivoDocumentoRepositorio archivoRepositorio,
    ITransmisionSunatRepositorio transmisionRepositorio,
    ISunatSummaryServiceCliente sunatCliente,
    IItemLoteDocumentoRepositorio itemRepositorio,
    IErrorDocumentoRepositorio errorRepositorio)
{
    private const string UsuarioWorker = "ms-facturacion-worker";

    public async Task<ResultadoOperacion<LoteDocumentoCreado>> EjecutarAsync(
        int idInquilino, int idEmpresa, DateOnly fechaReferencia, IReadOnlyList<ItemBajaEntrada> items,
        string ambienteCodigo, CancellationToken cancellationToken)
    {
        var fechaGeneracion = DateOnly.FromDateTime(RelojPeru.Ahora());
        var creado = await loteRepositorio.InsertarAsync(UsuarioWorker, idInquilino, idEmpresa, fechaReferencia, fechaGeneracion, items, cancellationToken);
        if (creado.IdTipoMensaje != TipoMensaje.Exito || creado.Datos is null)
        {
            return new ResultadoOperacion<LoteDocumentoCreado>(creado.IdTipoMensaje, creado.Mensaje, default);
        }

        var lote = await loteRepositorio.ObtenerAsync(idInquilino, creado.Datos.IdLoteDocumento, cancellationToken);
        if (lote.IdTipoMensaje != TipoMensaje.Exito || lote.Datos is null)
        {
            return new ResultadoOperacion<LoteDocumentoCreado>(lote.IdTipoMensaje, lote.Mensaje, default);
        }

        var empresa = await empresaRepositorio.ObtenerAsync(idInquilino, idEmpresa, cancellationToken);
        if (empresa.IdTipoMensaje != TipoMensaje.Exito || empresa.Datos is null)
        {
            return new ResultadoOperacion<LoteDocumentoCreado>(empresa.IdTipoMensaje, empresa.Mensaje, default);
        }

        var configuracion = await configuracionRepositorio.ObtenerPorEmpresaYAmbienteAsync(idInquilino, idEmpresa, ambienteCodigo, cancellationToken);
        if (configuracion.IdTipoMensaje != TipoMensaje.Exito || configuracion.Datos is null)
        {
            return new ResultadoOperacion<LoteDocumentoCreado>(configuracion.IdTipoMensaje, configuracion.Mensaje, default);
        }

        if (string.IsNullOrWhiteSpace(configuracion.Datos.UrlEnvioFacturaBoletaNota))
        {
            return ResultadoOperacion<LoteDocumentoCreado>.DeReglaDeNegocio(
                "La configuración de facturación de la empresa no tiene URL de envío (billService).");
        }

        var certificado = await proveedorCertificado.ObtenerAsync(idInquilino, idEmpresa, configuracion.Datos.IdCertificado, cancellationToken);
        if (certificado.IdTipoMensaje != TipoMensaje.Exito || certificado.Datos is null)
        {
            return new ResultadoOperacion<LoteDocumentoCreado>(certificado.IdTipoMensaje, certificado.Mensaje, default);
        }

        var claveSol = await credencialRepositorio.ObtenerPorTipoAsync(idInquilino, idEmpresa, "ClaveSol", cancellationToken);
        if (claveSol.IdTipoMensaje != TipoMensaje.Exito || claveSol.Datos is null)
        {
            return new ResultadoOperacion<LoteDocumentoCreado>(claveSol.IdTipoMensaje, claveSol.Mensaje, default);
        }

        var claveSolDescifrada = await cifradoServicio.DescifrarAsync(
            idInquilino, claveSol.Datos.ValorCifrado, claveSol.Datos.Nonce, claveSol.Datos.Tag, cancellationToken);
        if (claveSolDescifrada.IdTipoMensaje != TipoMensaje.Exito || claveSolDescifrada.Datos is null)
        {
            return new ResultadoOperacion<LoteDocumentoCreado>(claveSolDescifrada.IdTipoMensaje, claveSolDescifrada.Mensaje, default);
        }

        var xmlSinFirmar = constructorXml.Construir(lote.Datos, empresa.Datos);
        var xmlFirmado = firmador.Firmar(xmlSinFirmar, certificado.Datos);

        var nombreBase = $"{empresa.Datos.Ruc}-{lote.Datos.Cabecera.Nombre}";
        var nombreArchivoXml = $"{nombreBase}.xml";
        var nombreArchivoZip = $"{nombreBase}.zip";
        var zipBytes = empaquetador.Empaquetar(nombreArchivoXml, xmlFirmado);

        var idLoteDocumento = lote.Datos.Cabecera.IdLoteDocumento;
        var carpeta = $"{idInquilino}/{idEmpresa}/{fechaReferencia:yyyy}/{fechaReferencia:MM}/baja-{lote.Datos.Cabecera.Nombre}";
        var nombreAlmacenamiento = $"{lote.Datos.Cabecera.Nombre}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var idArchivoXml = await GuardarArchivoAsync(idInquilino, idLoteDocumento, carpeta, $"{nombreAlmacenamiento}.xml", xmlFirmado, "Xml", "application/xml", cancellationToken);
        var idArchivoZip = await GuardarArchivoAsync(idInquilino, idLoteDocumento, carpeta, $"{nombreAlmacenamiento}.zip", zipBytes, "Zip", "application/zip", cancellationToken);

        var usuarioSolCompleto = empresa.Datos.Ruc + claveSol.Datos.Usuario;

        var nuevaTransmision = new NuevaTransmisionSunat(
            null, idLoteDocumento, configuracion.Datos.TipoProveedorCodigo, configuracion.Datos.UrlEnvioFacturaBoletaNota,
            "sendSummary", idArchivoZip, 1, idArchivoXml);

        var transmision = await transmisionRepositorio.InsertarAsync(UsuarioWorker, idInquilino, nuevaTransmision, cancellationToken);
        if (transmision.IdTipoMensaje != TipoMensaje.Exito)
        {
            return new ResultadoOperacion<LoteDocumentoCreado>(transmision.IdTipoMensaje, transmision.Mensaje, default);
        }

        var envio = await sunatCliente.EnviarAsync(
            configuracion.Datos.UrlEnvioFacturaBoletaNota, usuarioSolCompleto, claveSolDescifrada.Datos, nombreArchivoZip, zipBytes, cancellationToken);

        if (envio.IdTipoMensaje != TipoMensaje.Exito || envio.Datos is null)
        {
            await transmisionRepositorio.ActualizarAsync(
                UsuarioWorker, idInquilino, transmision.Datos,
                new ResultadoTransmisionSunat(EstadoMaestroCodigo.ErrorSunat, null, null, null, envio.IdTipoMensaje.ToString(), envio.Mensaje),
                cancellationToken);

            // ReglaDeNegocio = SUNAT sí respondió al sendSummary, solo que sin ticket usable — un fallo real,
            // igual que el branch de TicketConError en ConsultarTicketComunicacionBajaCasoDeUso: se marca el
            // lote, cada documento (ComunicacionBajaConError) y se deja un registro en ERRORES_DOCUMENTO.
            // ErrorSistema = nunca hubo respuesta de SUNAT (ver el catch de SunatSummaryServiceCliente.EnviarAsync)
            // — no es un hecho sobre el documento ni sobre la baja en sí, así que el lote se deja en
            // PendienteEnvio (su estado de inserción) en vez de TicketConError, y ningún documento se toca.
            if (envio.IdTipoMensaje == TipoMensaje.ReglaDeNegocio)
            {
                await loteRepositorio.ActualizarEstadoSunatAsync(
                    UsuarioWorker, idInquilino, idLoteDocumento, EstadoMaestroCodigo.TicketConError, null, envio.IdTipoMensaje.ToString(), envio.Mensaje, cancellationToken);
                await itemRepositorio.ActualizarEstadoSunatTodosAsync(
                    UsuarioWorker, idInquilino, idLoteDocumento, EstadoMaestroCodigo.TicketConError, null, null, cancellationToken);

                var ahoraError = RelojPeru.Ahora();
                foreach (var item in lote.Datos.Items)
                {
                    await documentoRepositorio.ActualizarEstadoSunatAsync(
                        UsuarioWorker, idInquilino, item.IdDocumentoElectronico, EstadoMaestroCodigo.ComunicacionBajaConError,
                        null, null, null, null, ahoraError, cancellationToken);

                    await errorRepositorio.InsertarAsync(
                        UsuarioWorker, idInquilino,
                        new ErrorDocumento(item.IdDocumentoElectronico, transmision.Datos, "Sunat", string.Empty, envio.Mensaje, null, "Error"),
                        cancellationToken);
                }
            }

            return new ResultadoOperacion<LoteDocumentoCreado>(envio.IdTipoMensaje, envio.Mensaje, default);
        }

        await transmisionRepositorio.ActualizarAsync(
            UsuarioWorker, idInquilino, transmision.Datos,
            new ResultadoTransmisionSunat(EstadoMaestroCodigo.TicketRecibido, null, null, null, null, null),
            cancellationToken);

        await loteRepositorio.ActualizarEstadoSunatAsync(
            UsuarioWorker, idInquilino, lote.Datos.Cabecera.IdLoteDocumento, EstadoMaestroCodigo.TicketRecibido, envio.Datos, null, null, cancellationToken);

        // El lote ya refleja TicketRecibido, pero DOCUMENTOS_ELECTRONICOS.EstadoCodigo — lo que en
        // realidad lee el listado de facturas (SP_DocumentoElectronico_ListarParaPedidoFactura) — se
        // quedaba en Aceptado durante toda la ventana de espera de SUNAT, sin reflejar que hay una
        // Comunicación de Baja en curso. ComunicacionBajaEnviada existe en el catálogo (TABLA_MAESTRA
        // IdMaestro=1, Num1=6) justo para esto; faltaba aplicarla acá.
        var fechaActualizacion = RelojPeru.Ahora();
        foreach (var item in lote.Datos.Items)
        {
            await documentoRepositorio.ActualizarEstadoSunatAsync(
                UsuarioWorker, idInquilino, item.IdDocumentoElectronico, EstadoMaestroCodigo.ComunicacionBajaEnviada,
                null, null, null, null, fechaActualizacion, cancellationToken);
        }

        var resultado = new LoteDocumentoCreado(
            lote.Datos.Cabecera.IdLoteDocumento, lote.Datos.Cabecera.Nombre, "TicketRecibido", lote.Datos.Cabecera.FechaGeneracion);

        return ResultadoOperacion<LoteDocumentoCreado>.DeExito($"Comunicación de baja enviada, ticket: {envio.Datos}.", resultado);
    }

    private async Task<int> GuardarArchivoAsync(
        int idInquilino, int idLoteDocumento, string carpeta, string nombreArchivo, byte[] contenido, string tipoArchivoCodigo,
        string tipoContenido, CancellationToken cancellationToken)
    {
        var ruta = await almacenamiento.GuardarAsync(carpeta, nombreArchivo, contenido, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();

        var archivo = new ArchivoDocumento(null, idLoteDocumento, tipoArchivoCodigo, nombreArchivo, ruta, tipoContenido, hash, contenido.LongLength);
        var resultado = await archivoRepositorio.InsertarAsync(UsuarioWorker, idInquilino, archivo, cancellationToken);
        return resultado.Datos;
    }
}

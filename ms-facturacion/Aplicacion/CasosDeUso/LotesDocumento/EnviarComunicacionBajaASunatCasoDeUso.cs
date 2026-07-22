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
    ISunatSummaryServiceCliente sunatCliente)
{
    private const string UsuarioWorker = "ms-facturacion-worker";

    public async Task<ResultadoOperacion<LoteDocumentoCreado>> EjecutarAsync(
        int idInquilino, int idEmpresa, DateOnly fechaReferencia, IReadOnlyList<ItemBajaEntrada> items,
        string ambienteCodigo, CancellationToken cancellationToken)
    {
        var creado = await loteRepositorio.InsertarAsync(UsuarioWorker, idInquilino, idEmpresa, fechaReferencia, items, cancellationToken);
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
            idInquilino, claveSol.Datos.VersionLlave, claveSol.Datos.ValorCifrado, claveSol.Datos.Nonce, claveSol.Datos.Tag, cancellationToken);
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
        await GuardarArchivoAsync(idInquilino, idLoteDocumento, nombreArchivoXml, xmlFirmado, "Xml", "application/xml", cancellationToken);
        var idArchivoZip = await GuardarArchivoAsync(idInquilino, idLoteDocumento, nombreArchivoZip, zipBytes, "Zip", "application/zip", cancellationToken);

        var usuarioSolCompleto = empresa.Datos.Ruc + claveSol.Datos.Usuario;

        var nuevaTransmision = new NuevaTransmisionSunat(
            null, idLoteDocumento, configuracion.Datos.TipoProveedorCodigo, configuracion.Datos.UrlEnvioFacturaBoletaNota,
            "sendSummary", idArchivoZip, 1);

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
                new ResultadoTransmisionSunat("Error", null, null, null, envio.IdTipoMensaje.ToString(), envio.Mensaje),
                cancellationToken);

            return new ResultadoOperacion<LoteDocumentoCreado>(envio.IdTipoMensaje, envio.Mensaje, default);
        }

        await transmisionRepositorio.ActualizarAsync(
            UsuarioWorker, idInquilino, transmision.Datos,
            new ResultadoTransmisionSunat("TicketRecibido", null, null, null, null, null),
            cancellationToken);

        await loteRepositorio.ActualizarEstadoSunatAsync(
            UsuarioWorker, idInquilino, lote.Datos.Cabecera.IdLoteDocumento, "TicketRecibido", envio.Datos, null, null, cancellationToken);

        var resultado = new LoteDocumentoCreado(
            lote.Datos.Cabecera.IdLoteDocumento, lote.Datos.Cabecera.Nombre, "TicketRecibido", lote.Datos.Cabecera.FechaGeneracion);

        return ResultadoOperacion<LoteDocumentoCreado>.DeExito($"Comunicación de baja enviada, ticket: {envio.Datos}.", resultado);
    }

    private async Task<int> GuardarArchivoAsync(
        int idInquilino, int idLoteDocumento, string nombreArchivo, byte[] contenido, string tipoArchivoCodigo,
        string tipoContenido, CancellationToken cancellationToken)
    {
        var ruta = await almacenamiento.GuardarAsync(nombreArchivo, contenido, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();

        var archivo = new ArchivoDocumento(null, idLoteDocumento, tipoArchivoCodigo, nombreArchivo, ruta, tipoContenido, hash, contenido.LongLength);
        var resultado = await archivoRepositorio.InsertarAsync(UsuarioWorker, idInquilino, archivo, cancellationToken);
        return resultado.Datos;
    }
}

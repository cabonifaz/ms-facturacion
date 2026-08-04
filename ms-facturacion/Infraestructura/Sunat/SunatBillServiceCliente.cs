using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Sunat;

/// Envelope SOAP + WS-Security armado a mano (sin WCF) — sigue exactamente el ejemplo real de
/// facturacion/payload_input_output_sunat.md §2.2/§2.3. El parámetro usuarioSolCompleto ya debe venir
/// concatenado (EMPRESAS.Ruc + CREDENCIALES_INQUILINO.Usuario) — este cliente no conoce esa regla.
public sealed class SunatBillServiceCliente(
    HttpClient httpClient, IHostEnvironment entorno, ILogger<SunatBillServiceCliente> logger) : ISunatBillServiceCliente
{
    private static readonly XNamespace SoapEnv = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Ser = "http://service.sunat.gob.pe";
    private static readonly XNamespace Wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    public async Task<ResultadoOperacion<ResultadoEnvioSunat>> EnviarAsync(
        string url, string usuarioSolCompleto, string claveSol, string nombreArchivoZip, byte[] zipBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            if (entorno.IsDevelopment())
            {
                logger.LogInformation(
                    "sendBill — usuarioSolCompleto={UsuarioSolCompleto}, claveSol={ClaveSol}, fileName={NombreArchivoZip}, zipBytes={LongitudZip} bytes.",
                    usuarioSolCompleto, claveSol, nombreArchivoZip, zipBytes.Length);

                var rutaDebug = Path.Combine(Path.GetTempPath(), "ms-facturacion-debug", nombreArchivoZip);
                Directory.CreateDirectory(Path.GetDirectoryName(rutaDebug)!);
                await File.WriteAllBytesAsync(rutaDebug, zipBytes, cancellationToken);
                logger.LogInformation("sendBill — ZIP de salida guardado en {RutaDebug} para inspección manual.", rutaDebug);
            }

            var sobreEnvio = ConstruirSobreEnvio(usuarioSolCompleto, claveSol, nombreArchivoZip, zipBytes);

            if (entorno.IsDevelopment())
            {
                logger.LogInformation("sendBill — envelope enviado (contraseña redactada):\n{Envelope}", RedactarClave(sobreEnvio));
            }

            var xmlSobreEnvio = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + sobreEnvio.ToString(SaveOptions.DisableFormatting);
            using var contenido = new StringContent(xmlSobreEnvio, Encoding.UTF8, "text/xml");
            contenido.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };

            using var solicitud = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = contenido,
                Version = HttpVersion.Version11,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact
            };
            solicitud.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");

            using var respuesta = await httpClient.SendAsync(solicitud, cancellationToken);
            var cuerpoRespuesta = await respuesta.Content.ReadAsStringAsync(cancellationToken);

            if (entorno.IsDevelopment())
            {
                logger.LogInformation(
                    "sendBill — HTTP {StatusCode}. Respuesta cruda de SUNAT:\n{CuerpoRespuesta}",
                    (int)respuesta.StatusCode, cuerpoRespuesta);
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                var faultString = ExtraerFaultString(cuerpoRespuesta);
                return ResultadoOperacion<ResultadoEnvioSunat>.DeReglaDeNegocio(
                    faultString ?? $"SUNAT respondió con error HTTP {(int)respuesta.StatusCode}.");
            }

            return InterpretarRespuesta(cuerpoRespuesta);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<ResultadoEnvioSunat>.DeErrorSistema(ex.Message);
        }
    }

    private static XDocument ConstruirSobreEnvio(string usuarioSolCompleto, string claveSol, string nombreArchivoZip, byte[] zipBytes)
    {
        var sobre = new XElement(SoapEnv + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", SoapEnv.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "ser", Ser.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "wsse", Wsse.NamespaceName),
            new XElement(SoapEnv + "Header",
                new XElement(Wsse + "Security",
                    new XElement(Wsse + "UsernameToken",
                        new XElement(Wsse + "Username", usuarioSolCompleto),
                        new XElement(Wsse + "Password",
                            new XAttribute("Type", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText"),
                            claveSol)))),
            new XElement(SoapEnv + "Body",
                new XElement(Ser + "sendBill",
                    new XElement("fileName", nombreArchivoZip),
                    new XElement("contentFile", Convert.ToBase64String(zipBytes)))));

        return new XDocument(sobre);
    }

    private ResultadoOperacion<ResultadoEnvioSunat> InterpretarRespuesta(string cuerpoRespuesta)
    {
        var documento = XDocument.Parse(cuerpoRespuesta);

        var faultString = ExtraerFaultString(cuerpoRespuesta);
        if (faultString is not null)
        {
            return ResultadoOperacion<ResultadoEnvioSunat>.DeReglaDeNegocio(faultString);
        }

        var applicationResponseBase64 = documento.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "applicationResponse")?.Value;

        if (string.IsNullOrWhiteSpace(applicationResponseBase64))
        {
            return ResultadoOperacion<ResultadoEnvioSunat>.DeErrorSistema(
                "La respuesta de SUNAT no contiene 'applicationResponse'.");
        }

        var cdrZipBytes = Convert.FromBase64String(applicationResponseBase64);
        var cdrXmlBytes = ExtraerXmlDelZip(cdrZipBytes);

        var cdrDocumento = XDocument.Parse(Encoding.UTF8.GetString(cdrXmlBytes));
        var respuestaNodo = cdrDocumento.Descendants(Cac + "Response").FirstOrDefault();

        var codigoRespuesta = respuestaNodo?.Element(Cbc + "ResponseCode")?.Value ?? string.Empty;
        var descripcionRespuesta = respuestaNodo?.Element(Cbc + "Description")?.Value ?? string.Empty;

        var estadoCodigo = MapearEstadoCodigo(codigoRespuesta);

        var resultado = new ResultadoEnvioSunat(estadoCodigo, codigoRespuesta, descripcionRespuesta, cdrZipBytes, cdrXmlBytes);
        return ResultadoOperacion<ResultadoEnvioSunat>.DeExito("SUNAT procesó el envío.", resultado);
    }

    /// Rangos de ResponseCode: 0 = Aceptado, 2000-3999 = Rechazado, 4000+ = AceptadoConObservaciones
    /// (ver flujo_tablas_microservicio_facturacion_sunat.md §10 / payload_input_output_sunat.md §2.3).
    private static EstadoMaestroCodigo MapearEstadoCodigo(string codigoRespuesta)
    {
        if (codigoRespuesta == "0")
        {
            return EstadoMaestroCodigo.Aceptado;
        }

        if (int.TryParse(codigoRespuesta, out var codigo))
        {
            if (codigo is >= 2000 and <= 3999)
            {
                return EstadoMaestroCodigo.Rechazado;
            }

            if (codigo >= 4000)
            {
                return EstadoMaestroCodigo.AceptadoConObservaciones;
            }
        }

        return EstadoMaestroCodigo.Rechazado;
    }

    /// Copia el envelope reemplazando la contraseña por "***" y el Base64 del zip por su tamaño — para poder
    /// loguear la estructura real del envelope (namespaces, orden de elementos) sin exponer la Clave SOL.
    private static XDocument RedactarClave(XDocument sobreEnvio)
    {
        var copia = new XDocument(sobreEnvio);

        var passwordElemento = copia.Descendants(Wsse + "Password").FirstOrDefault();
        if (passwordElemento is not null)
        {
            passwordElemento.Value = "***";
        }

        var contentFileElemento = copia.Descendants("contentFile").FirstOrDefault();
        if (contentFileElemento is not null)
        {
            contentFileElemento.Value = $"(BASE64 redactado, {contentFileElemento.Value.Length} caracteres)";
        }

        return copia;
    }

    private static byte[] ExtraerXmlDelZip(byte[] zipBytes)
    {
        using var memoria = new MemoryStream(zipBytes);
        using var zip = new ZipArchive(memoria, ZipArchiveMode.Read);
        var entrada = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("El CDR de SUNAT no contiene ningún archivo .xml.");

        using var entradaStream = entrada.Open();
        using var salida = new MemoryStream();
        entradaStream.CopyTo(salida);
        return salida.ToArray();
    }

    private static string? ExtraerFaultString(string cuerpoRespuesta)
    {
        try
        {
            var documento = XDocument.Parse(cuerpoRespuesta);
            return documento.Descendants().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }
}

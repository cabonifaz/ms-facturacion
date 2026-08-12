using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Sunat;

/// sendSummary/getStatus — mismo billService, mismo WS-Security UsernameToken que sendBill
/// (ver SunatBillServiceCliente); solo cambian el nombre de la operación SOAP y la forma de la respuesta.
public sealed class SunatSummaryServiceCliente(
    HttpClient httpClient, IHostEnvironment entorno, ILogger<SunatSummaryServiceCliente> logger) : ISunatSummaryServiceCliente
{
    private static readonly XNamespace SoapEnv = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Ser = "http://service.sunat.gob.pe";
    private static readonly XNamespace Wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    public async Task<ResultadoOperacion<string>> EnviarAsync(
        string url, string usuarioSolCompleto, string claveSol, string nombreArchivoZip, byte[] zipBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            var sobre = ConstruirSobre(usuarioSolCompleto, claveSol,
                new XElement(Ser + "sendSummary",
                    new XElement("fileName", nombreArchivoZip),
                    new XElement("contentFile", Convert.ToBase64String(zipBytes))));

            var cuerpoRespuesta = await EnviarSobreAsync(url, sobre, "sendSummary", cancellationToken);
            if (cuerpoRespuesta is null)
            {
                return ResultadoOperacion<string>.DeErrorSistema("No se pudo conectar con SUNAT.");
            }

            var faultString = ExtraerFaultString(cuerpoRespuesta);
            if (faultString is not null)
            {
                return ResultadoOperacion<string>.DeReglaDeNegocio(faultString);
            }

            var documento = XDocument.Parse(cuerpoRespuesta);
            var ticket = documento.Descendants().FirstOrDefault(e => e.Name.LocalName == "ticket")?.Value;

            if (string.IsNullOrWhiteSpace(ticket))
            {
                // DeReglaDeNegocio, no DeErrorSistema — SUNAT sí respondió (pasamos el chequeo de cuerpo nulo
                // y de faultString), solo que sin 'ticket'. Mismo criterio que SunatBillServiceCliente:
                // DeErrorSistema queda reservado para cuando nunca hubo respuesta de SUNAT (ver el catch de
                // acá abajo y el "cuerpoRespuesta is null" más arriba), para que EnviarComunicacionBajaASunatCasoDeUso
                // pueda distinguir "nunca llegamos a SUNAT" de "SUNAT respondió mal" por este TipoMensaje.
                return ResultadoOperacion<string>.DeReglaDeNegocio("La respuesta de SUNAT no contiene 'ticket'.");
            }

            return ResultadoOperacion<string>.DeExito("SUNAT recibió la comunicación, en proceso.", ticket);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<string>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<ResultadoConsultaTicket>> ConsultarAsync(
        string url, string usuarioSolCompleto, string claveSol, string ticket, CancellationToken cancellationToken)
    {
        try
        {
            var sobre = ConstruirSobre(usuarioSolCompleto, claveSol,
                new XElement(Ser + "getStatus", new XElement("ticket", ticket)));

            var cuerpoRespuesta = await EnviarSobreAsync(url, sobre, "getStatus", cancellationToken);
            if (cuerpoRespuesta is null)
            {
                return ResultadoOperacion<ResultadoConsultaTicket>.DeErrorSistema("No se pudo conectar con SUNAT.");
            }

            var faultString = ExtraerFaultString(cuerpoRespuesta);
            if (faultString is not null)
            {
                return ResultadoOperacion<ResultadoConsultaTicket>.DeReglaDeNegocio(faultString);
            }

            var documento = XDocument.Parse(cuerpoRespuesta);
            var statusCode = documento.Descendants().FirstOrDefault(e => e.Name.LocalName == "statusCode")?.Value;

            if (string.IsNullOrWhiteSpace(statusCode))
            {
                return ResultadoOperacion<ResultadoConsultaTicket>.DeErrorSistema("La respuesta de SUNAT no contiene 'statusCode'.");
            }

            // 98 = en proceso, todavía no hay CDR.
            if (statusCode == "98")
            {
                return ResultadoOperacion<ResultadoConsultaTicket>.DeExito(
                    "SUNAT todavía está procesando el ticket.",
                    new ResultadoConsultaTicket(EstadoMaestroCodigo.TicketPendiente, statusCode, null, null));
            }

            // 99 = terminó con error, sin CDR utilizable.
            if (statusCode == "99")
            {
                return ResultadoOperacion<ResultadoConsultaTicket>.DeExito(
                    "SUNAT terminó de procesar el ticket con error.",
                    new ResultadoConsultaTicket(EstadoMaestroCodigo.TicketConError, statusCode, null, null));
            }

            // 0 = procesado, viene el CDR en base64 dentro de <content>.
            var contentBase64 = documento.Descendants().FirstOrDefault(e => e.Name.LocalName == "content")?.Value;
            if (string.IsNullOrWhiteSpace(contentBase64))
            {
                return ResultadoOperacion<ResultadoConsultaTicket>.DeErrorSistema(
                    "SUNAT indicó el ticket como procesado pero no devolvió 'content' con el CDR.");
            }

            var cdrZipBytes = Convert.FromBase64String(contentBase64);
            var cdrXmlBytes = ExtraerXmlDelZip(cdrZipBytes);

            var cdrDocumento = XDocument.Parse(Encoding.UTF8.GetString(cdrXmlBytes));
            var respuestaNodo = cdrDocumento.Descendants(Cac + "Response").FirstOrDefault();
            var codigoRespuesta = respuestaNodo?.Element(Cbc + "ResponseCode")?.Value ?? string.Empty;
            var descripcionRespuesta = respuestaNodo?.Element(Cbc + "Description")?.Value ?? string.Empty;

            var estadoCodigo = MapearEstadoCodigo(codigoRespuesta);

            return ResultadoOperacion<ResultadoConsultaTicket>.DeExito(
                "SUNAT terminó de procesar el ticket.",
                new ResultadoConsultaTicket(estadoCodigo, codigoRespuesta, descripcionRespuesta, cdrXmlBytes));
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<ResultadoConsultaTicket>.DeErrorSistema(ex.Message);
        }
    }

    /// Comunicación de baja aceptada/rechazada usa sus propios estados terminales (ComunicacionBajaAceptada/
    /// ComunicacionBajaRechazada) en vez de "Aceptado"/"Rechazado" — esos ya significan "el documento en sí
    /// fue aceptado/rechazado por SUNAT", algo distinto de "la solicitud de anularlo fue aceptada/rechazada"
    /// (el documento sigue siendo válido si la baja es rechazada). Mismo mapeo de rangos que sendBill
    /// (0/2000-3999/4000+, ver payload_input_output_sunat.md §2.3).
    private static EstadoMaestroCodigo MapearEstadoCodigo(string codigoRespuesta)
    {
        if (codigoRespuesta == "0")
        {
            return EstadoMaestroCodigo.ComunicacionBajaAceptada;
        }

        if (int.TryParse(codigoRespuesta, out var codigo))
        {
            if (codigo is >= 2000 and <= 3999)
            {
                return EstadoMaestroCodigo.ComunicacionBajaRechazada;
            }

            if (codigo >= 4000)
            {
                return EstadoMaestroCodigo.AceptadoConObservaciones;
            }
        }

        return EstadoMaestroCodigo.ComunicacionBajaRechazada;
    }

    private static XDocument ConstruirSobre(string usuarioSolCompleto, string claveSol, XElement cuerpoOperacion) =>
        new(new XElement(SoapEnv + "Envelope",
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
            new XElement(SoapEnv + "Body", cuerpoOperacion)));

    private async Task<string?> EnviarSobreAsync(string url, XDocument sobre, string nombreOperacion, CancellationToken cancellationToken)
    {
        using var contenido = new StringContent(sobre.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml");
        contenido.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, url) { Content = contenido };
        solicitud.Headers.TryAddWithoutValidation("SOAPAction", "");

        using var respuesta = await httpClient.SendAsync(solicitud, cancellationToken);
        var cuerpoRespuesta = await respuesta.Content.ReadAsStringAsync(cancellationToken);

        if (entorno.IsDevelopment())
        {
            logger.LogInformation(
                "{NombreOperacion} — HTTP {StatusCode}. Respuesta cruda de SUNAT:\n{CuerpoRespuesta}",
                nombreOperacion, (int)respuesta.StatusCode, cuerpoRespuesta);
        }

        return cuerpoRespuesta;
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

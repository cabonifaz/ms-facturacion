using Microsoft.AspNetCore.Mvc;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Controllers;

public sealed record FormaPagoPeticion(string Codigo, IReadOnlyList<CuotaPeticion>? Cuotas);
public sealed record CuotaPeticion(int NumeroCuota, DateOnly FechaVencimiento, decimal Monto);
public sealed record ClientePeticion(string TipoDocumentoCodigo, string NumeroDocumento, string? Nombre, string? Correo, string? Direccion);
public sealed record DocumentoAfectadoPeticion(int IdDocumentoElectronicoRelacionado, string TipoReferenciaCodigo, string MotivoCodigo, string MotivoDescripcion);

public sealed record ItemPeticion(
    int NumeroLinea, string ProductoCodigo, string? ProductoSunatCodigo, string Descripcion, string UnidadMedidaCodigo,
    decimal Cantidad, decimal ValorUnitario, decimal PrecioUnitario, decimal MontoDescuento,
    string AfectacionIgvCodigo, decimal PorcentajeIgv);

public sealed record InsertarDocumentoElectronicoPeticion(
    int IdInquilino, int IdEmpresa, string SistemaOrigen, string IdExterno, string TipoDocumentoCodigo,
    int IdSerieDocumento, DateOnly FechaEmision, TimeOnly HoraEmision, string MonedaCodigo, string TipoOperacionCodigo,
    FormaPagoPeticion FormaPago, ClientePeticion Cliente, DocumentoAfectadoPeticion? DocumentoAfectado,
    IReadOnlyList<ItemPeticion> Items, string AmbienteCodigo);

public sealed record ActualizarEstadoSunatPeticion(
    string EstadoCodigo, string? SunatHash, string? SunatCodigoRespuesta, string? SunatDescripcionRespuesta, string? SunatTicket);

[ApiController]
[Route("api/v1/documentos-electronicos")]
public sealed class DocumentosElectronicosController(
    InsertarDocumentoElectronicoCasoDeUso insertarCasoDeUso,
    ObtenerDocumentoElectronicoCasoDeUso obtenerCasoDeUso,
    ListarDocumentosElectronicosCasoDeUso listarCasoDeUso,
    ActualizarEstadoSunatDocumentoElectronicoCasoDeUso actualizarEstadoSunatCasoDeUso,
    EnviarDocumentoElectronicoASunatCasoDeUso enviarASunatCasoDeUso) : ControllerBase
{
    // TODO: reemplazar por el usuario ejecutor real una vez definida la autenticación servicio-a-servicio con maximlian3_backend.
    private const string UsuarioEjecutor = "ms-facturacion";

    [HttpPost]
    public async Task<IActionResult> Insertar(InsertarDocumentoElectronicoPeticion peticion, CancellationToken cancellationToken)
    {
        var cliente = new ClienteDatosEntrada(
            peticion.Cliente.TipoDocumentoCodigo, peticion.Cliente.NumeroDocumento,
            peticion.Cliente.Nombre, peticion.Cliente.Correo, peticion.Cliente.Direccion);

        var documentoAfectado = peticion.DocumentoAfectado is null
            ? null
            : new DocumentoAfectadoEntrada(
                peticion.DocumentoAfectado.IdDocumentoElectronicoRelacionado, peticion.DocumentoAfectado.TipoReferenciaCodigo,
                peticion.DocumentoAfectado.MotivoCodigo, peticion.DocumentoAfectado.MotivoDescripcion);

        var lineas = peticion.Items
            .Select(item => new LineaDocumentoElectronicoEntrada(
                item.NumeroLinea, item.ProductoCodigo, item.ProductoSunatCodigo, item.Descripcion, item.UnidadMedidaCodigo,
                item.Cantidad, item.ValorUnitario, item.PrecioUnitario, item.MontoDescuento, item.AfectacionIgvCodigo, item.PorcentajeIgv))
            .ToList();

        var cuotas = (peticion.FormaPago.Cuotas ?? [])
            .Select(cuota => new CuotaDocumentoElectronico(cuota.NumeroCuota, cuota.FechaVencimiento, cuota.Monto))
            .ToList();

        var resultado = await insertarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, peticion.IdInquilino, peticion.IdEmpresa, peticion.SistemaOrigen, peticion.IdExterno,
            peticion.TipoDocumentoCodigo, peticion.IdSerieDocumento, peticion.FechaEmision, peticion.HoraEmision,
            peticion.MonedaCodigo, peticion.TipoOperacionCodigo, peticion.FormaPago.Codigo, cliente,
            documentoAfectado, lineas, cuotas, cancellationToken);

        if (resultado.IdTipoMensaje != TipoMensaje.Exito || resultado.Datos is null)
        {
            return ResponderSegunEnvelope(resultado);
        }

        // sendBill (Factura/Boleta/Nota de Crédito/Nota de Débito) se resuelve de forma síncrona dentro
        // del mismo request, porque SUNAT devuelve el CDR casi de inmediato para estos tipos. sendSummary
        // (resumen/baja) y el resto de tipos quedan en PendienteEnvio para un envío posterior — eso es
        // Módulo 4 sin terminar todavía (ver flujo_tablas_microservicio_facturacion_sunat.md, camino
        // híbrido sync/async).
        if (peticion.TipoDocumentoCodigo is not ("01" or "03" or "07" or "08"))
        {
            return ResponderSegunEnvelope(resultado);
        }

        var envioSunat = await enviarASunatCasoDeUso.EjecutarAsync(
            peticion.IdInquilino, resultado.Datos.IdDocumentoElectronico, peticion.AmbienteCodigo, cancellationToken);

        return ResponderInsertarConEnvioSunat(resultado, envioSunat);
    }

    [HttpGet("{idDocumentoElectronico:int}")]
    public async Task<IActionResult> Obtener(
        [FromQuery] int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        var resultado = await obtenerCasoDeUso.EjecutarAsync(idInquilino, idDocumentoElectronico, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int idInquilino, [FromQuery] int idEmpresa, [FromQuery] string? estadoCodigo, [FromQuery] string? busqueda,
        [FromQuery] DateOnly? fechaDesde, [FromQuery] DateOnly? fechaHasta, [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 20, CancellationToken cancellationToken = default)
    {
        var resultado = await listarCasoDeUso.EjecutarAsync(
            idInquilino, idEmpresa, estadoCodigo, busqueda, fechaDesde, fechaHasta, pagina, tamanoPagina, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    // Uso exclusivo del Worker (Módulo 4) — no es un Actualizar genérico, solo aplica la respuesta de SUNAT.
    [HttpPut("{idDocumentoElectronico:int}/estado-sunat")]
    public async Task<IActionResult> ActualizarEstadoSunat(
        [FromQuery] int idInquilino, int idDocumentoElectronico, ActualizarEstadoSunatPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await actualizarEstadoSunatCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, idInquilino, idDocumentoElectronico, peticion.EstadoCodigo, peticion.SunatHash,
            peticion.SunatCodigoRespuesta, peticion.SunatDescripcionRespuesta, peticion.SunatTicket, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    // Módulo 4 (Worker): dispara el camino síncrono sendBill (Factura/Boleta/Nota de Crédito/Nota de Débito) para un documento en PendienteEnvio/Error.
    [HttpPost("{idDocumentoElectronico:int}/enviar-sunat")]
    public async Task<IActionResult> EnviarASunat(
        [FromQuery] int idInquilino, int idDocumentoElectronico, [FromQuery] string ambienteCodigo, CancellationToken cancellationToken)
    {
        var resultado = await enviarASunatCasoDeUso.EjecutarAsync(idInquilino, idDocumentoElectronico, ambienteCodigo, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    /// Combina el resultado de Insertar (el documento ya quedó persistido, siempre) con el de enviar-sunat.
    /// El código HTTP refleja el desenlace de SUNAT, no el de Insertar — pero el cuerpo siempre incluye el
    /// documento creado, para que el llamador pueda reintentar el envío por separado si SUNAT falló.
    private IActionResult ResponderInsertarConEnvioSunat(
        ResultadoOperacion<DocumentoElectronicoCreado> insertado, ResultadoOperacion<ResultadoEnvioSunat> envioSunat)
    {
        var cuerpo = new
        {
            Documento = insertado.Datos,
            Sunat = new { envioSunat.Mensaje, Datos = envioSunat.Datos }
        };

        return envioSunat.IdTipoMensaje switch
        {
            TipoMensaje.Exito => Ok(cuerpo),
            TipoMensaje.ReglaDeNegocio => BadRequest(cuerpo),
            _ => StatusCode(StatusCodes.Status500InternalServerError, cuerpo)
        };
    }

    private IActionResult ResponderSegunEnvelope<T>(ResultadoOperacion<T> resultado) => resultado.IdTipoMensaje switch
    {
        TipoMensaje.Exito => Ok(new { resultado.Mensaje, Datos = resultado.Datos }),
        TipoMensaje.ReglaDeNegocio => BadRequest(new { resultado.Mensaje }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new { resultado.Mensaje })
    };
}

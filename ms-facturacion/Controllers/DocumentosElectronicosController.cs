using Microsoft.AspNetCore.Mvc;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Controllers;

public sealed record FormaPagoPeticion(int IdFormaPago, IReadOnlyList<CuotaPeticion>? Cuotas);
public sealed record CuotaPeticion(int NumeroCuota, DateOnly FechaVencimiento, decimal Monto);
public sealed record ClientePeticion(
    int IdTipoDocumentoSunat, string NumeroDocumento, string? Nombre, string? Correo, string? Direccion, int PaisCodigo);
public sealed record DocumentoAfectadoPeticion(int IdDocumentoElectronicoRelacionado, string TipoReferenciaCodigo, string MotivoCodigo, string MotivoDescripcion);

public sealed record ItemPeticion(
    int NumeroLinea, string ProductoCodigo, string? ProductoSunatCodigo, string Descripcion, int IdUnidadMedidaMaestro,
    decimal Cantidad, decimal ValorUnitario, decimal MontoDescuento,
    int IdAfectacionIgvMaestro, decimal PorcentajeIgv);

public sealed record InsertarDocumentoElectronicoPeticion(
    int IdInquilino, int IdEmpresa, string IdExterno, string? NumeroReferencia, int IdTipoDocumentoMaestro,
    int IdMonedaMaestro, decimal? TipoCambio, int IdTipoOperacionMaestro,
    FormaPagoPeticion FormaPago, ClientePeticion Cliente, DocumentoAfectadoPeticion? DocumentoAfectado,
    IReadOnlyList<ItemPeticion> Items, IReadOnlyList<CampoExtraPeticion>? CamposExtra = null);

public sealed record ActualizarEstadoSunatPeticion(
    EstadoMaestroCodigo EstadoCodigo, string? SunatHash, string? SunatCodigoRespuesta, string? SunatDescripcionRespuesta, string? SunatTicket);

/// Línea dentro de "Guardar cambios" en lote — IdLineaDocumentoElectronico es 0 (u omitido) para una línea
/// nueva, o el id existente para actualizar una ya guardada. Una línea que no venga en el arreglo se da de baja.
public sealed record LineaEdicionPeticion(
    string ProductoCodigo, string? ProductoSunatCodigo, string Descripcion, int IdUnidadMedidaMaestro,
    decimal Cantidad, decimal ValorUnitario, decimal MontoDescuento,
    int IdAfectacionIgvMaestro, decimal PorcentajeIgv, int NumeroLinea, int IdLineaDocumentoElectronico = 0);

/// Cuota dentro de "Guardar cambios" en lote — mismo criterio de IdCuotaDocumentoElectronico que LineaEdicionPeticion.
public sealed record CuotaEdicionPeticion(
    DateOnly FechaVencimiento, decimal Monto, int NumeroCuota, int IdCuotaDocumentoElectronico = 0);

/// Campo extra dentro de "Guardar cambios" en lote — mismo criterio de IdCampoExtraDocumentoElectronico
/// que LineaEdicionPeticion/CuotaEdicionPeticion.
public sealed record CampoExtraEdicionPeticion(string Texto, int IdCampoExtraDocumentoElectronico = 0);

public sealed record GuardarCambiosDocumentoElectronicoPeticion(
    int IdFormaPago, string? NumeroReferencia, int IdMonedaMaestro, decimal? TipoCambio, int IdTipoOperacionMaestro,
    IReadOnlyList<LineaEdicionPeticion> Lineas, IReadOnlyList<CuotaEdicionPeticion> Cuotas,
    IReadOnlyList<CampoExtraEdicionPeticion>? CamposExtra = null);

public sealed record ActualizarEstadoCuotaPeticion(EstadoCuotaCodigo EstadoCuotaCodigo);

[ApiController]
[Route("api/v1/documentos-electronicos")]
public sealed class DocumentosElectronicosController(
    InsertarDocumentoElectronicoCasoDeUso insertarCasoDeUso,
    ObtenerDocumentoElectronicoCasoDeUso obtenerCasoDeUso,
    ListarDocumentosElectronicosCasoDeUso listarCasoDeUso,
    ListarDocumentosElectronicosParaPedidoFacturaCasoDeUso listarParaPedidoFacturaCasoDeUso,
    ListarDocumentosParaSireRvieCasoDeUso listarParaSireRvieCasoDeUso,
    GenerarTxtSireRvieCasoDeUso generarTxtSireRvieCasoDeUso,
    ActualizarEstadoSunatDocumentoElectronicoCasoDeUso actualizarEstadoSunatCasoDeUso,
    EnviarDocumentoElectronicoASunatCasoDeUso enviarASunatCasoDeUso,
    GuardarCambiosDocumentoElectronicoCasoDeUso guardarCambiosCasoDeUso,
    ActualizarEstadoCuotaDocumentoElectronicoCasoDeUso actualizarEstadoCuotaCasoDeUso,
    ListarEventosRecientesCasoDeUso listarEventosRecientesCasoDeUso,
    ListarErroresUltimoEnvioCasoDeUso listarErroresUltimoEnvioCasoDeUso,
    ObtenerUrlDescargaDocumentoCasoDeUso obtenerUrlDescargaCasoDeUso,
    ObtenerDocumentoElectronicoPorTokenCasoDeUso obtenerPorTokenCasoDeUso,
    ObtenerUrlDescargaPorTokenCasoDeUso obtenerUrlDescargaPorTokenCasoDeUso,
    ObtenerTokenVerificacionDocumentoCasoDeUso obtenerTokenVerificacionCasoDeUso,
    ObtenerClienteDocumentoElectronicoCasoDeUso obtenerClienteCasoDeUso,
    IHostEnvironment entorno) : ControllerBase
{
    // TODO: reemplazar por el usuario ejecutor real una vez definida la autenticación servicio-a-servicio con maximlian3_backend.
    private const string UsuarioEjecutor = "ms-facturacion";

    [HttpPost]
    public async Task<IActionResult> Insertar(InsertarDocumentoElectronicoPeticion peticion, CancellationToken cancellationToken)
    {
        var cliente = new ClienteDatosEntrada(
            peticion.Cliente.IdTipoDocumentoSunat, peticion.Cliente.NumeroDocumento,
            peticion.Cliente.Nombre, peticion.Cliente.Correo, peticion.Cliente.Direccion, peticion.Cliente.PaisCodigo);

        var documentoAfectado = peticion.DocumentoAfectado is null
            ? null
            : new DocumentoAfectadoEntrada(
                peticion.DocumentoAfectado.IdDocumentoElectronicoRelacionado, peticion.DocumentoAfectado.TipoReferenciaCodigo,
                peticion.DocumentoAfectado.MotivoCodigo, peticion.DocumentoAfectado.MotivoDescripcion);

        var lineas = peticion.Items
            .Select(item => new LineaDocumentoElectronicoEntrada(
                item.NumeroLinea, item.ProductoCodigo, item.ProductoSunatCodigo, item.Descripcion, item.IdUnidadMedidaMaestro,
                item.Cantidad, item.ValorUnitario, item.MontoDescuento, item.IdAfectacionIgvMaestro, item.PorcentajeIgv))
            .ToList();

        var cuotas = (peticion.FormaPago.Cuotas ?? [])
            .Select(cuota => new CuotaDocumentoElectronico(cuota.NumeroCuota, cuota.FechaVencimiento, cuota.Monto))
            .ToList();

        var camposExtra = (peticion.CamposExtra ?? [])
            .Select(c => new CampoExtraEntrada(c.Texto))
            .ToList();

        var resultado = await insertarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, peticion.IdInquilino, peticion.IdEmpresa, peticion.IdExterno, peticion.NumeroReferencia,
            peticion.IdTipoDocumentoMaestro,
            peticion.IdMonedaMaestro, peticion.TipoCambio, peticion.IdTipoOperacionMaestro, peticion.FormaPago.IdFormaPago, cliente,
            documentoAfectado, lineas, cuotas, camposExtra, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    // "Guardar" deja el documento en PendienteEnvio, editable — el llamador manda el estado final deseado
    // de líneas y cuotas (una sola vez, con el botón "Guardar cambios") y el SP calcula el diff
    // (insertar/actualizar/dar de baja) mientras no se haya confirmado el envío (ver EnviarASunat,
    // "Confirmar con SUNAT"). Reemplaza el diseño anterior de 6 endpoints granulares por línea/cuota.
    [HttpPut("{idDocumentoElectronico:int}/guardar-cambios")]
    public async Task<IActionResult> GuardarCambios(
        [FromQuery] int idInquilino, int idDocumentoElectronico,
        GuardarCambiosDocumentoElectronicoPeticion peticion, CancellationToken cancellationToken)
    {
        var lineas = peticion.Lineas
            .Select(linea => new LineaDocumentoElectronicoEntrada(
                linea.NumeroLinea, linea.ProductoCodigo, linea.ProductoSunatCodigo, linea.Descripcion, linea.IdUnidadMedidaMaestro,
                linea.Cantidad, linea.ValorUnitario, linea.MontoDescuento,
                linea.IdAfectacionIgvMaestro, linea.PorcentajeIgv, linea.IdLineaDocumentoElectronico))
            .ToList();

        var cuotas = peticion.Cuotas
            .Select(cuota => new CuotaDocumentoElectronico(
                cuota.NumeroCuota, cuota.FechaVencimiento, cuota.Monto, cuota.IdCuotaDocumentoElectronico))
            .ToList();

        var camposExtra = (peticion.CamposExtra ?? [])
            .Select(c => new CampoExtraEntrada(c.Texto, c.IdCampoExtraDocumentoElectronico))
            .ToList();

        var resultado = await guardarCambiosCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, idInquilino, idDocumentoElectronico, peticion.IdFormaPago, peticion.NumeroReferencia,
            peticion.IdMonedaMaestro, peticion.TipoCambio, peticion.IdTipoOperacionMaestro, lineas, cuotas, camposExtra, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    // Marca el estado de pago de una cuota (Pendiente/Pagado) — no requiere que el documento esté en
    // ningún EstadoCodigo particular: el pago puede ocurrir mucho después de que SUNAT ya aceptó el documento.
    [HttpPut("{idDocumentoElectronico:int}/cuotas/{idCuotaDocumentoElectronico:int}/estado")]
    public async Task<IActionResult> ActualizarEstadoCuota(
        [FromQuery] int idInquilino, int idDocumentoElectronico, int idCuotaDocumentoElectronico,
        ActualizarEstadoCuotaPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await actualizarEstadoCuotaCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, idInquilino, idDocumentoElectronico, idCuotaDocumentoElectronico,
            peticion.EstadoCuotaCodigo, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet("{idDocumentoElectronico:int}")]
    public async Task<IActionResult> Obtener(
        [FromQuery] int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        var resultado = await obtenerCasoDeUso.EjecutarAsync(idInquilino, idDocumentoElectronico, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    // tipoArchivo: "Xml" o "Pdf". Devuelve una URL presignada de S3 (vigencia 5 min), no el archivo en sí.
    [HttpGet("{idDocumentoElectronico:int}/url-descarga")]
    public async Task<IActionResult> ObtenerUrlDescarga(
        [FromQuery] int idInquilino, int idDocumentoElectronico, [FromQuery] string tipoArchivo, CancellationToken cancellationToken)
    {
        var resultado = await obtenerUrlDescargaCasoDeUso.EjecutarAsync(idInquilino, idDocumentoElectronico, tipoArchivo, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    // Puerta de entrada de la verificación pública: dado solo el token (el "código de verificación" del
    // PDF), sin idInquilino, sin autenticación de usuario. maximlian3_backend es quien la expone sin
    // requerir login — acá sigue detrás del X-Api-Key normal (único llamador válido es maximlian3_backend).
    [HttpGet("token/{token}")]
    public async Task<IActionResult> ObtenerPorToken(string token, CancellationToken cancellationToken)
    {
        var resultado = await obtenerPorTokenCasoDeUso.EjecutarAsync(token, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    // tipoArchivo: "Xml" o "Pdf". Mismo criterio que ObtenerPorToken.
    [HttpGet("token/{token}/url-descarga")]
    public async Task<IActionResult> ObtenerUrlDescargaPorToken(
        string token, [FromQuery] string tipoArchivo, CancellationToken cancellationToken)
    {
        var resultado = await obtenerUrlDescargaPorTokenCasoDeUso.EjecutarAsync(token, tipoArchivo, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    // Para que maximlian3_backend arme el link de verificación pública ({frontendBaseUrl}/{token}) —
    // el token nunca se expone vía Obtener (ver SP_DocumentoElectronico_Obtener), solo acá.
    [HttpGet("{idDocumentoElectronico:int}/token-verificacion")]
    public async Task<IActionResult> ObtenerTokenVerificacion(
        [FromQuery] int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        var resultado = await obtenerTokenVerificacionCasoDeUso.EjecutarAsync(idInquilino, idDocumentoElectronico, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    // Snapshot de cliente de un documento ya emitido, sin resolver — para prellenar el receptor de una
    // Nota de Crédito/Débito con el mismo cliente del documento afectado (ver documentoAfectado).
    [HttpGet("{idDocumentoElectronico:int}/cliente")]
    public async Task<IActionResult> ObtenerCliente(
        [FromQuery] int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        var resultado = await obtenerClienteCasoDeUso.EjecutarAsync(idInquilino, idDocumentoElectronico, cancellationToken);
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

    // Exclusivo para el listado que maximlian3_backend expone desde PedidoFactura — no reutiliza Listar
    // (distinto shape/filtros, ver SP_DocumentoElectronico_ListarParaPedidoFactura).
    [HttpGet("para-pedido-factura")]
    public async Task<IActionResult> ListarParaPedidoFactura(
        [FromQuery] int idInquilino, [FromQuery] int idEmpresa, [FromQuery] string? estadoCodigo,
        [FromQuery] int? idFormaPago, [FromQuery] DateOnly? fechaDesde, [FromQuery] DateOnly? fechaHasta,
        [FromQuery] string? busqueda, [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = 20,
        CancellationToken cancellationToken = default)
    {
        var resultado = await listarParaPedidoFacturaCasoDeUso.EjecutarAsync(
            idInquilino, idEmpresa, estadoCodigo, idFormaPago, fechaDesde, fechaHasta, busqueda, pagina, tamanoPagina, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    // SIRE RVIE (Formato 14.4) — documentos de un período ya con todos los campos resueltos para el
    // generador del TXT (ver SP_DocumentoElectronico_ListarParaSireRvie y SIRE_RVIE_Estructura_Campos.md).
    [HttpGet("sire-rvie")]
    public async Task<IActionResult> ListarParaSireRvie(
        [FromQuery] int idInquilino, [FromQuery] int idEmpresa, [FromQuery] DateOnly periodo,
        CancellationToken cancellationToken)
    {
        var resultado = await listarParaSireRvieCasoDeUso.EjecutarAsync(idInquilino, idEmpresa, periodo, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    // Devuelve el TXT ya armado (no el JSON de arriba) — descarga directa, listo para comprimir en ZIP y
    // subir al módulo SIRE. Codificado en ISO-8859-1 (ver GeneradorSireRvieServicio).
    [HttpGet("sire-rvie/txt")]
    public async Task<IActionResult> GenerarTxtSireRvie(
        [FromQuery] int idInquilino, [FromQuery] int idEmpresa, [FromQuery] DateOnly periodo,
        CancellationToken cancellationToken)
    {
        var resultado = await generarTxtSireRvieCasoDeUso.EjecutarAsync(idInquilino, idEmpresa, periodo, cancellationToken);
        if (resultado.IdTipoMensaje != TipoMensaje.Exito || resultado.Datos is null)
        {
            return ResponderSegunEnvelope(resultado);
        }

        return File(resultado.Datos.Contenido, "text/plain", resultado.Datos.NombreArchivo);
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
    // AmbienteCodigo (Beta/Produccion) se deriva del entorno real del servidor, no de un valor mandado por
    // el llamador — así una request no puede hacer que esta instancia le pegue al SUNAT equivocado.
    [HttpPost("{idDocumentoElectronico:int}/enviar-sunat")]
    public async Task<IActionResult> EnviarASunat(
        [FromQuery] int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        var ambienteCodigo = entorno.IsDevelopment() || entorno.IsStaging() ? "Beta" : "Produccion";
        var resultado = await enviarASunatCasoDeUso.EjecutarAsync(idInquilino, idDocumentoElectronico, ambienteCodigo, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    // Para que maximlian3_backend sincronice PEDIDO_FACTURA sondeando EVENTOS_DOCUMENTO desde un checkpoint.
    [HttpGet("eventos-recientes")]
    public async Task<IActionResult> ListarEventosRecientes(
        [FromQuery] int idInquilino, [FromQuery] int ultimoIdEvento, CancellationToken cancellationToken)
    {
        var resultado = await listarEventosRecientesCasoDeUso.EjecutarAsync(idInquilino, ultimoIdEvento, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    // Solo los errores/observaciones del último intento de envío a SUNAT (no el historial completo de
    // reintentos anteriores) — ver SP_ErrorDocumento_ListarUltimoEnvio.
    [HttpGet("{idDocumentoElectronico:int}/errores-ultimo-envio")]
    public async Task<IActionResult> ListarErroresUltimoEnvio(
        [FromQuery] int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        var resultado = await listarErroresUltimoEnvioCasoDeUso.EjecutarAsync(idInquilino, idDocumentoElectronico, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    private IActionResult ResponderSegunEnvelope<T>(ResultadoOperacion<T> resultado) => resultado.IdTipoMensaje switch
    {
        TipoMensaje.Exito => Ok(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje, Datos = resultado.Datos }),
        TipoMensaje.ReglaDeNegocio => BadRequest(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje })
    };
}

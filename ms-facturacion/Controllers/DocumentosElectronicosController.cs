using Microsoft.AspNetCore.Mvc;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Controllers;

public sealed record FormaPagoPeticion(int? IdFormaPago, IReadOnlyList<CuotaPeticion>? Cuotas);
public sealed record CuotaPeticion(int NumeroCuota, DateOnly FechaVencimiento, decimal Monto, int IdEstadoCuotaMaestro, DateTime? FechaPago);
public sealed record ClientePeticion(
    int IdTipoDocumentoSunat, string NumeroDocumento, string? Nombre, string? Correo, string? Direccion, int PaisCodigo);
public sealed record DocumentoAfectadoPeticion(int IdDocumentoElectronicoRelacionado, int IdMotivoMaestro);

public sealed record ItemPeticion(
    int NumeroLinea, string? ProductoCodigo, string? ProductoSunatCodigo, string Descripcion, int IdUnidadMedidaMaestro,
    decimal Cantidad, decimal ValorUnitario, decimal MontoDescuento,
    int IdAfectacionIgvMaestro, decimal PorcentajeIgv);

public sealed record InsertarDocumentoElectronicoPeticion(
    int IdInquilino, int IdEmpresa, string IdExterno, string? NumeroReferencia, int IdTipoDocumentoMaestro,
    int IdMonedaMaestro, decimal? TipoCambio, int IdTipoOperacionMaestro,
    FormaPagoPeticion? FormaPago, ClientePeticion Cliente, DocumentoAfectadoPeticion? DocumentoAfectado,
    IReadOnlyList<ItemPeticion> Items, IReadOnlyList<CampoExtraPeticion>? CamposExtra = null);

public sealed record ActualizarEstadoSunatPeticion(
    EstadoMaestroCodigo EstadoCodigo, string? SunatHash, string? SunatCodigoRespuesta, string? SunatDescripcionRespuesta, string? SunatTicket);

/// FechaAnulacion es la fecha real en que ocurrió la anulación en SUNAT (normalmente se descubre después
/// de que pasó) — no se resuelve con RelojPeru.Ahora() como el resto de fechas server-authoritative de este
/// controller, porque acá el llamador sí conoce el dato real y no "ahora".
public sealed record AnularManualmentePeticion(string Motivo, DateTime FechaAnulacion);

/// Línea dentro de "Guardar cambios" en lote — IdLineaDocumentoElectronico es 0 (u omitido) para una línea
/// nueva, o el id existente para actualizar una ya guardada. Una línea que no venga en el arreglo se da de baja.
public sealed record LineaEdicionPeticion(
    string? ProductoCodigo, string? ProductoSunatCodigo, string Descripcion, int IdUnidadMedidaMaestro,
    decimal Cantidad, decimal ValorUnitario, decimal MontoDescuento,
    int IdAfectacionIgvMaestro, decimal PorcentajeIgv, int NumeroLinea, int IdLineaDocumentoElectronico = 0);

/// Cuota dentro de "Guardar cambios" en lote — mismo criterio de IdCuotaDocumentoElectronico que LineaEdicionPeticion.
public sealed record CuotaEdicionPeticion(
    DateOnly FechaVencimiento, decimal Monto, int NumeroCuota, int IdEstadoCuotaMaestro, DateTime? FechaPago,
    int IdCuotaDocumentoElectronico = 0);

/// Campo extra dentro de "Guardar cambios" en lote — mismo criterio de IdCampoExtraDocumentoElectronico
/// que LineaEdicionPeticion/CuotaEdicionPeticion.
public sealed record CampoExtraEdicionPeticion(string Texto, int IdCampoExtraDocumentoElectronico = 0);

public sealed record GuardarCambiosDocumentoElectronicoPeticion(
    string IdExterno, int? IdFormaPago, string? NumeroReferencia, int IdMonedaMaestro, decimal? TipoCambio, int IdTipoOperacionMaestro,
    IReadOnlyList<LineaEdicionPeticion> Lineas, IReadOnlyList<CuotaEdicionPeticion> Cuotas,
    IReadOnlyList<CampoExtraEdicionPeticion>? CamposExtra = null, int? IdMotivoMaestro = null);

public sealed record ActualizarEstadoCuotaPeticion(EstadoCuotaCodigo EstadoCuotaCodigo, DateTime? FechaPago);

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
    AnularManualmenteDocumentoElectronicoCasoDeUso anularManualmenteCasoDeUso,
    PrevisualizarAnulacionManualCasoDeUso previsualizarAnulacionManualCasoDeUso,
    EnviarDocumentoElectronicoASunatCasoDeUso enviarASunatCasoDeUso,
    GuardarCambiosDocumentoElectronicoCasoDeUso guardarCambiosCasoDeUso,
    ActualizarEstadoCuotaDocumentoElectronicoCasoDeUso actualizarEstadoCuotaCasoDeUso,
    EliminarBorradorDocumentoElectronicoCasoDeUso eliminarBorradorCasoDeUso,
    ListarEventosRecientesCasoDeUso listarEventosRecientesCasoDeUso,
    ListarErroresUltimoEnvioCasoDeUso listarErroresUltimoEnvioCasoDeUso,
    ObtenerUrlDescargaDocumentoCasoDeUso obtenerUrlDescargaCasoDeUso,
    ObtenerDocumentoElectronicoPorTokenCasoDeUso obtenerPorTokenCasoDeUso,
    ObtenerIdDocumentoElectronicoPorTokenCasoDeUso obtenerIdPorTokenCasoDeUso,
    ObtenerUrlDescargaPorTokenCasoDeUso obtenerUrlDescargaPorTokenCasoDeUso,
    ObtenerTokenVerificacionDocumentoCasoDeUso obtenerTokenVerificacionCasoDeUso,
    ObtenerParaNotaCasoDeUso obtenerParaNotaCasoDeUso,
    ObtenerResumenFacturacionCasoDeUso obtenerResumenFacturacionCasoDeUso,
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
                peticion.DocumentoAfectado.IdDocumentoElectronicoRelacionado, peticion.DocumentoAfectado.IdMotivoMaestro);

        var lineas = peticion.Items
            .Select(item => new LineaDocumentoElectronicoEntrada(
                item.NumeroLinea, item.ProductoCodigo, item.ProductoSunatCodigo, item.Descripcion, item.IdUnidadMedidaMaestro,
                item.Cantidad, item.ValorUnitario, item.MontoDescuento, item.IdAfectacionIgvMaestro, item.PorcentajeIgv))
            .ToList();

        var cuotas = (peticion.FormaPago?.Cuotas ?? [])
            .Select(cuota => new CuotaDocumentoElectronicoEntrada(
                cuota.NumeroCuota, cuota.FechaVencimiento, cuota.Monto, cuota.IdEstadoCuotaMaestro, cuota.FechaPago))
            .ToList();

        var camposExtra = (peticion.CamposExtra ?? [])
            .Select(c => new CampoExtraEntrada(c.Texto))
            .ToList();

        var resultado = await insertarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, peticion.IdInquilino, peticion.IdEmpresa, peticion.IdExterno, peticion.NumeroReferencia,
            peticion.IdTipoDocumentoMaestro,
            peticion.IdMonedaMaestro, peticion.TipoCambio, peticion.IdTipoOperacionMaestro, peticion.FormaPago?.IdFormaPago, cliente,
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
            .Select(cuota => new CuotaDocumentoElectronicoEntrada(
                cuota.NumeroCuota, cuota.FechaVencimiento, cuota.Monto, cuota.IdEstadoCuotaMaestro, cuota.FechaPago,
                cuota.IdCuotaDocumentoElectronico))
            .ToList();

        var camposExtra = (peticion.CamposExtra ?? [])
            .Select(c => new CampoExtraEntrada(c.Texto, c.IdCampoExtraDocumentoElectronico))
            .ToList();

        var resultado = await guardarCambiosCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, idInquilino, idDocumentoElectronico, peticion.IdExterno, peticion.IdFormaPago, peticion.NumeroReferencia,
            peticion.IdMonedaMaestro, peticion.TipoCambio, peticion.IdTipoOperacionMaestro, peticion.IdMotivoMaestro,
            lineas, cuotas, camposExtra, cancellationToken);
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
            peticion.EstadoCuotaCodigo, peticion.FechaPago, cancellationToken);
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

    // Variante mínima de ObtenerPorToken: solo el Id/IdInquilino, para que maximlian3_backend arme
    // su propia consulta (pedidos del documento) contra su base. Mismo criterio de exposición
    // pública que ObtenerPorToken.
    [HttpGet("token/{token}/id")]
    public async Task<IActionResult> ObtenerIdPorToken(string token, CancellationToken cancellationToken)
    {
        var resultado = await obtenerIdPorTokenCasoDeUso.EjecutarAsync(token, cancellationToken);
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

    // Cliente + listado de productos de un documento ya emitido, sin resolver — para prellenar el receptor
    // y listar los productos del documento afectado al armar una Nota de Crédito/Débito.
    [HttpGet("{idDocumentoElectronico:int}/para-nota")]
    public async Task<IActionResult> ObtenerParaNota(
        [FromQuery] int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        var resultado = await obtenerParaNotaCasoDeUso.EjecutarAsync(idInquilino, idDocumentoElectronico, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    // Deshabilitado — sin caller (maximlian3_backend usa ListarParaPedidoFactura, no este). Comentado en
    // vez de borrado para retomarlo en un sprint futuro sin tener que reescribirlo.
    /*
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
    */

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

    // Dashboard de PedidoFactura en maximlian3_backend — ver SP_DocumentoElectronico_ObtenerResumenFacturacion.
    [HttpGet("resumen")]
    public async Task<IActionResult> ObtenerResumenFacturacion(
        [FromQuery] int idInquilino, [FromQuery] int idEmpresa, [FromQuery] DateOnly? fechaDesde,
        [FromQuery] DateOnly? fechaHasta, CancellationToken cancellationToken)
    {
        var resultado = await obtenerResumenFacturacionCasoDeUso.EjecutarAsync(
            idInquilino, idEmpresa, fechaDesde, fechaHasta, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    // Deshabilitado — sin caller (maximlian3_backend solo usa sire-rvie/txt, no este JSON intermedio).
    // Comentado en vez de borrado para retomarlo en un sprint futuro sin tener que reescribirlo.
    /*
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
    */

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

    // Deshabilitado — sin caller HTTP (EnviarDocumentoElectronicoASunatCasoDeUso ya llama
    // documentoRepositorio.ActualizarEstadoSunatAsync directo, en el mismo proceso, sin pasar por este
    // endpoint). Comentado en vez de borrado para retomarlo en un sprint futuro sin tener que reescribirlo.
    /*
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
    */

    // Para cuando el usuario descubre que SUNAT ya muestra el documento como anulado sin que este sistema
    // haya tramitado esa baja (p.ej. anulado directo en el portal de SUNAT) — registra esa anulación acá,
    // con motivo y la fecha real en que ocurrió.
    [HttpPut("{idDocumentoElectronico:int}/anular-manualmente")]
    public async Task<IActionResult> AnularManualmente(
        [FromQuery] int idInquilino, int idDocumentoElectronico, AnularManualmentePeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await anularManualmenteCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, idInquilino, idDocumentoElectronico, peticion.Motivo, peticion.FechaAnulacion, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    // Elimina (soft-delete) un borrador que nunca se envió a SUNAT (PendienteEnvio) — Factura, Boleta, Nota
    // de Crédito o Nota de Débito. Ver SP_DocumentoElectronico_EliminarBorrador.
    [HttpDelete("{idDocumentoElectronico:int}")]
    public async Task<IActionResult> EliminarBorrador(
        [FromQuery] int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        var resultado = await eliminarBorradorCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, idInquilino, idDocumentoElectronico, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    // Previsualiza AnularManualmente sin ejecutar nada — mismas validaciones (documento elegible, sin Nota
    // de Crédito/Débito sin resolver) y, de poder ejecutarse, la lista de documentos que se verían afectados
    // (el propio + las Notas vigentes que se arrastrarían).
    [HttpGet("{idDocumentoElectronico:int}/anular-manualmente/preview")]
    public async Task<IActionResult> PrevisualizarAnulacionManual(
        [FromQuery] int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        var resultado = await previsualizarAnulacionManualCasoDeUso.EjecutarAsync(idInquilino, idDocumentoElectronico, cancellationToken);

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

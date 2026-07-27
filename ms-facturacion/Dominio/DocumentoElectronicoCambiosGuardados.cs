namespace ms_facturacion.Dominio;

/// Resultado de "Guardar cambios" en lote — líneas y cuotas ya con el diff aplicado (SP_DocumentoElectronico_GuardarCambios).
public sealed record DocumentoElectronicoCambiosGuardados(
    IReadOnlyList<LineaDocumentoElectronico> Lineas, IReadOnlyList<CuotaDocumentoElectronico> Cuotas);

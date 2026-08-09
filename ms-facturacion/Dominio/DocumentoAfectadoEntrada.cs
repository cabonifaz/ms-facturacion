namespace ms_facturacion.Dominio;

/// Obligatorio para notas de crédito/débito (07/08), prohibido para 01/03 — validado en la API, no aquí.
public sealed record DocumentoAfectadoEntrada(
    int IdDocumentoElectronicoRelacionado, string MotivoCodigo, string MotivoDescripcion);

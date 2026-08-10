namespace ms_facturacion.Dominio;

/// Obligatorio para notas de crédito/débito (07/08), prohibido para 01/03 — validado en la API, no aquí.
/// IdMotivoMaestro = Num1 de TABLA_MAESTRA IdMaestro=14 (Nota de Crédito, Catálogo N.° 09 SUNAT) o 15
/// (Nota de Débito, Catálogo N.° 10 SUNAT) — mismo criterio que el resto de campos de catálogo del
/// proyecto (se guarda/valida el Num1, se resuelve al string SUNAT solo al leer). MotivoDescripcion no
/// viaja acá — SP_DocumentoElectronico_Insertar la resuelve internamente desde TABLA_MAESTRA.String2.
public sealed record DocumentoAfectadoEntrada(int IdDocumentoElectronicoRelacionado, int IdMotivoMaestro);

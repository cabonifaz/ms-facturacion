namespace ms_facturacion.Dominio;

/// Estados de TABLA_MAESTRA IdMaestro=7 — los valores numéricos coinciden con Num1 en
/// 03_LlenarTablaMaestra_MsFacturacion.sql. Transición única y unidireccional en el uso normal
/// (Pendiente -> Pagado), independiente del EstadoCodigo del documento en SUNAT.
public enum EstadoCuotaCodigo
{
    Pendiente = 1,
    Pagado = 2
}

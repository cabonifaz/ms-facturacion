namespace ms_facturacion.Dominio;

/// Proyección liviana para listados — SP_SerieDocumento_Listar solo devuelve estas columnas.
public sealed record SerieDocumentoResumen(int IdSerieDocumento, string TipoDocumentoCodigo, string Serie, int NumeroActual, bool Activo);

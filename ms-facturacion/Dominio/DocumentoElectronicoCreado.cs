namespace ms_facturacion.Dominio;

public sealed record DocumentoElectronicoCreado(
    int IdDocumentoElectronico, string Serie, int Correlativo, string EstadoCodigo, DateTime FechaCreacion);

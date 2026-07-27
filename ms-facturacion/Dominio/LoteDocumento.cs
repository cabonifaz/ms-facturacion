namespace ms_facturacion.Dominio;

public sealed record LoteDocumento(
    int IdLoteDocumento, int IdEmpresa, string TipoLoteCodigo, string Nombre, DateOnly FechaReferencia,
    DateTime FechaGeneracion, string EstadoCodigo, string? Ticket, string? SunatCodigoRespuesta,
    string? SunatDescripcionRespuesta);

public sealed record ItemLoteDocumentoDetalle(
    int IdItemLoteDocumento, int IdDocumentoElectronico, int NumeroLinea, string MotivoDescripcion,
    string EstadoItemCodigo, string TipoDocumentoCodigo, string Serie, int Correlativo);

public sealed record LoteDocumentoDetalle(LoteDocumento Cabecera, IReadOnlyList<ItemLoteDocumentoDetalle> Items);

/// Input para SP_LoteDocumento_Insertar — un documento a incluir en la comunicación de baja.
public sealed record ItemBajaEntrada(int IdDocumentoElectronico, string MotivoDescripcion);

public sealed record LoteDocumentoCreado(int IdLoteDocumento, string Nombre, string EstadoCodigo, DateTime FechaGeneracion);

namespace ms_facturacion.Aplicacion.Comun;

public sealed record ResultadoPaginado<T>(int TotalRegistros, int TotalPaginas, IReadOnlyList<T> Items);

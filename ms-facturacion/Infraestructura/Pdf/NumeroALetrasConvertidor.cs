using System.Globalization;

namespace ms_facturacion.Infraestructura.Pdf;

/// Convierte un monto a su representación en letras (Catálogo N.° 52 SUNAT, código 1000 "Monto en Letras")
/// — p.ej. 1250.75 → "MIL DOSCIENTOS CINCUENTA CON 75/100 SOLES". Soporta hasta 999,999,999.99.
public static class NumeroALetrasConvertidor
{
    private static readonly string[] Unidades =
        ["", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE"];

    private static readonly string[] Decenas =
        ["DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE"];

    private static readonly string[] Decenas2 =
        ["", "", "VEINTE", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"];

    private static readonly string[] Centenas =
        ["", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS",
         "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"];

    public static string Convertir(decimal monto, string monedaCodigo)
    {
        var enteros = (long)Math.Truncate(monto);
        var centimos = (int)Math.Round((monto - enteros) * 100, MidpointRounding.AwayFromZero);

        var nombreMoneda = monedaCodigo switch
        {
            "USD" => "DÓLARES AMERICANOS",
            _ => "SOLES"
        };

        var letras = enteros == 0 ? "CERO" : ConvertirEnteros(enteros);
        return $"SON: {letras} CON {centimos:00}/100 {nombreMoneda}";
    }

    private static string ConvertirEnteros(long numero)
    {
        if (numero == 0) return "";
        if (numero == 1) return "UN";

        if (numero < 10) return Unidades[numero];
        if (numero < 20) return Decenas[numero - 10];
        if (numero < 30) return numero == 20 ? "VEINTE" : $"VEINTI{Unidades[numero % 10].ToLowerInvariant()}".ToUpperInvariant();
        if (numero < 100)
        {
            var decena = Decenas2[numero / 10];
            var unidad = numero % 10;
            return unidad == 0 ? decena : $"{decena} Y {Unidades[unidad]}";
        }
        if (numero < 1000)
        {
            var centena = numero / 100;
            var resto = numero % 100;
            if (numero == 100) return "CIEN";
            var prefijo = Centenas[centena];
            return resto == 0 ? prefijo : $"{prefijo} {ConvertirEnteros(resto)}";
        }
        if (numero < 1_000_000)
        {
            var miles = numero / 1000;
            var resto = numero % 1000;
            var prefijoMiles = miles == 1 ? "MIL" : $"{ConvertirEnteros(miles)} MIL";
            return resto == 0 ? prefijoMiles : $"{prefijoMiles} {ConvertirEnteros(resto)}";
        }
        if (numero < 1_000_000_000)
        {
            var millones = numero / 1_000_000;
            var resto = numero % 1_000_000;
            var prefijoMillones = millones == 1 ? "UN MILLÓN" : $"{ConvertirEnteros(millones)} MILLONES";
            return resto == 0 ? prefijoMillones : $"{prefijoMillones} {ConvertirEnteros(resto)}";
        }

        throw new ArgumentOutOfRangeException(nameof(numero), "Monto fuera de rango soportado (máximo 999,999,999).");
    }
}

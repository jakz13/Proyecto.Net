using System.Diagnostics;
using System.Globalization;
using UltimaMilla.Mobile.Services.Optimization;
using UltimaMilla.Mobile.ViewModels.Ruta;

// Comparacion medida: criterio estandar de despacho (cierre de ventana + codigo, espejo del RF 16 del backend)
// contra la optimizacion de la app (vecino mas cercano + 2-opt con penalizacion de ventana).
var servicio = new RutaOptimizationService();
var ventanas = new[] { "09:00 - 12:00", "12:00 - 15:00", "15:00 - 18:00" };
var tamanos = new[] { 8, 15, 25, 40 };
const int casos = 300;

Console.WriteLine("| Paradas | Casos | Distancia base media (km) | Distancia optimizada media (km) | Ahorro medio | Ahorro mediano | Ahorro min - max | Pares de paradas fuera del orden de ventana | Tiempo de calculo medio |");
Console.WriteLine("|---|---|---|---|---|---|---|---|---|");

foreach (var n in tamanos)
{
    var ahorros = new List<double>(); double inversiones = 0, pares = 0; double baseTotal = 0, optTotal = 0; double ms = 0; int ventanaIncumplida = 0;
    for (int seed = 0; seed < casos; seed++)
    {
        var rnd = new Random(1000 * n + seed);
        var paradas = Enumerable.Range(0, n).Select(i => new ParadaItemViewModel
        {
            CodigoSeguimiento = $"TRK-{rnd.Next(0x10000000, int.MaxValue):X8}",
            Latitud = -34.95 + rnd.NextDouble() * 0.12,    // Montevideo y area metropolitana cercana
            Longitud = -56.30 + rnd.NextDouble() * 0.22,
            VentanaHoraria = ventanas[rnd.Next(ventanas.Length)],
            Estado = "Pendiente"
        }).ToList();

        var sw = Stopwatch.StartNew();
        var r = servicio.OptimizarOrdenParadasAsync(paradas).GetAwaiter().GetResult();
        sw.Stop();
        ms += sw.Elapsed.TotalMilliseconds;
        baseTotal += r.DistanciaBaseKm; optTotal += r.DistanciaOptimizadaKm;
        ahorros.Add(r.PorcentajeAhorro);
        var cierres = r.SecuenciaOptimizada.Select(p => int.Parse(p.VentanaHoraria.Split('-')[1].Trim().Substring(0, 2))).ToList();
        for (int i = 0; i < cierres.Count; i++) for (int j = i + 1; j < cierres.Count; j++) { pares++; if (cierres[i] > cierres[j]) inversiones++; }
    }
    ahorros.Sort();
    string f(double v) => v.ToString("F1", CultureInfo.InvariantCulture);
    Console.WriteLine($"| {n} | {casos} | {f(baseTotal / casos)} | {f(optTotal / casos)} | {f((baseTotal - optTotal) / baseTotal * 100)} % | {f(ahorros[ahorros.Count / 2])} % | {f(ahorros[0])} - {f(ahorros[^1])} % | {inversiones / pares * 100:F1} % | {ms / casos:F1} ms |");
}

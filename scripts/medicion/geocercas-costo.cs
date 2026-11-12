using System.Diagnostics;

// Costo de evaluar las geocercas (Haversine) de una ruta contra una posicion GPS: lo unico que agregan las geocercas al rastreo.
static double Haversine(double lat1, double lon1, double lat2, double lon2)
{
    const double R = 6371000.0;
    double dLat = (lat2 - lat1) * Math.PI / 180.0, dLon = (lon2 - lon1) * Math.PI / 180.0;
    double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
    return 2 * R * Math.Asin(Math.Sqrt(a));
}

var rnd = new Random(42);
foreach (var paradas in new[] { 10, 30, 60 })
{
    var pts = Enumerable.Range(0, paradas).Select(_ => (-34.95 + rnd.NextDouble() * 0.1, -56.25 + rnd.NextDouble() * 0.15)).ToArray();
    const int evaluaciones = 200_000;
    double acumulado = 0;
    for (int w = 0; w < 5_000; w++) foreach (var p in pts) acumulado += Haversine(-34.9, -56.18, p.Item1, p.Item2); // calentamiento
    var sw = Stopwatch.StartNew();
    for (int i = 0; i < evaluaciones; i++)
    {
        double lat = -34.95 + (i % 1000) * 0.0001, lon = -56.25 + (i % 700) * 0.0001;
        foreach (var p in pts) acumulado += Haversine(lat, lon, p.Item1, p.Item2);
    }
    sw.Stop();
    Console.WriteLine($"{paradas} paradas: {sw.Elapsed.TotalMilliseconds * 1000.0 / evaluaciones:F2} us por posicion GPS (control {acumulado:F0})");
}

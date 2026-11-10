// Pruebas de carga con k6 (opcional 7.4). Escenarios: rampa (consulta pública), concurrencia sostenida (alta por API) y
// sincronización simultánea de repartidores que regresan al depósito.
//
//   k6 run -e BASE_URL=https://shippingbox.duckdns.org scripts/carga/carga.js
//   docker run --rm -i -v "$PWD/scripts/carga:/scripts" grafana/k6 run -e BASE_URL=http://host.docker.internal:5000 /scripts/carga.js
//
// Variables: BASE_URL, REPARTIDORES (default 10), CLAVE_API (default: clave demo del operador 1), ESCENARIOS (lista separada por comas).
// Las pruebas de integración y la rampa dependen de datos sembrados: usar un ambiente con el sembrado de demostración.
import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Counter, Trend } from 'k6/metrics';

const BASE = __ENV.BASE_URL || 'http://localhost:5000';
const N = parseInt(__ENV.REPARTIDORES || '10', 10);
const CLAVE_API = __ENV.CLAVE_API || 'dev-api-key-megastore-2026';
const ESCENARIOS = (__ENV.ESCENARIOS || 'seguimiento,alta,sync').split(',');
const JSON_H = { 'Content-Type': 'application/json' };

const limitadas = new Counter('solicitudes_limitadas_429');
const duracionSync = new Trend('sincronizacion_repartidor_ms', true);

const todos = {
  // Rampa: del tráfico normal al pico. La consulta pública está limitada por IP (30/min): los 429 son el comportamiento esperado
  // de la protección y se cuentan aparte; lo que se mide es la latencia de lo atendido y que no haya errores 5xx.
  seguimiento: {
    executor: 'ramping-vus', exec: 'consultaPublica', startVUs: 0,
    stages: [{ duration: '20s', target: 10 }, { duration: '40s', target: 50 }, { duration: '40s', target: 100 }, { duration: '20s', target: 0 }],
  },
  // Concurrencia sostenida: 1,5 altas por segundo (90/min) con la clave de producción, por debajo de su cupo de 120/min.
  alta: {
    executor: 'constant-arrival-rate', exec: 'altaPorApi', rate: 3, timeUnit: '2s', duration: '90s',
    preAllocatedVUs: 10, maxVUs: 30,
  },
  // Duración: tráfico leve y constante durante 3 minutos (detecta fugas de memoria y degradación con el tiempo).
  duracion: {
    executor: 'constant-vus', exec: 'consultaPublica', vus: 2, duration: '3m',
  },
  // Sincronización simultánea: N repartidores regresan al depósito y sincronizan a la vez (descargan la ruta, envían su retiro y se rinde).
  sync: {
    executor: 'per-vu-iterations', exec: 'sincronizacion', vus: N, iterations: 1, maxDuration: '2m', startTime: '2s',
  },
};

export const options = {
  scenarios: Object.fromEntries(Object.entries(todos).filter(([k]) => ESCENARIOS.includes(k))),
  // Objetivos de nivel de servicio definidos de antemano
  thresholds: {
    'http_req_duration{escenario:seguimiento}': ['p(95)<300', 'p(99)<800'],
    'http_req_duration{escenario:alta}': ['p(95)<800'],
    'http_req_failed{escenario:alta}': ['rate<0.01'],
    'checks{escenario:sync}': ['rate==1'],
    'sincronizacion_repartidor_ms': ['p(95)<2000'],
    'checks{escenario:seguimiento}': ['rate>0.99'],
  },
};

function post(url, cuerpo, token, etiquetas) {
  const cabeceras = Object.assign({}, JSON_H, token ? { Authorization: `Bearer ${token}` } : {});
  return http.post(`${BASE}${url}`, JSON.stringify(cuerpo), { headers: cabeceras, tags: etiquetas });
}

function login(email, password) {
  const r = post('/api/v1/auth/login', { email, password });
  if (r.status !== 200) throw new Error(`login ${email}: ${r.status} ${r.body}`);
  return r.json('token');
}

// ----- Preparación: crea N repartidores con una hoja de ruta de retiro despachada cada uno -----
export function setup() {
  if (!ESCENARIOS.includes('sync') && !ESCENARIOS.includes('seguimiento') && !ESCENARIOS.includes('duracion')) return {};

  const datos = { reps: [], admin: null, codigoSeguimiento: null };
  const admin = login('admin@gmail.com', 'Admin.2026!');
  datos.admin = admin;

  // Un código de seguimiento real para la consulta pública
  const alta = post('/api/integracion/v1/envios', {
    modalidadServicio: 'Estandar', nombreDestinatario: 'Carga', calle: 'Av. Italia', numero: '1', ciudad: 'Montevideo',
    bultos: [{ peso: 1, alto: 10, ancho: 10, profundo: 10 }], referenciaExterna: `carga-${Date.now()}`,
  }, null);
  if (alta.status === 201 || alta.status === 200) datos.codigoSeguimiento = alta.json('codigoSeguimiento');

  if (!ESCENARIOS.includes('sync')) return datos;

  const comercio = login('comercio@gmail.com', 'Comercio.2026!');
  const fecha = new Date().toISOString().slice(0, 10);
  const sufijo = Date.now().toString(36);

  for (let i = 0; i < N; i++) {
    const email = `carga.${sufijo}.${i}@ultimamilla.com.uy`;
    const vehiculo = post('/api/v1/flota/vehiculos', { patente: `P${String(i).padStart(2, "0")}${sufijo.slice(-4)}`.toUpperCase(), tipo: 'Furgón', capacidadPeso: 500, capacidadVolumen: 10 }, admin);
    const repartidor = post('/api/v1/flota/repartidores', { nombreCompleto: `Repartidor carga ${i}`, email, documento: '4.111.222-3', telefono: '099000000', password: 'Repartidor.2026!', vehiculoAsignadoId: vehiculo.json('id') }, admin);
    const retiro = post('/api/comercio/retiros', {
      nombreContacto: 'Ana', calle: 'Av. Italia', numero: String(10 + i), ciudad: 'Montevideo', motivo: 'Carga',
      bultos: [{ descripcion: 'Bulto A', peso: 1, alto: 10, ancho: 10, profundo: 10 }],
    }, comercio);
    const ruta = post('/api/planificacion/hojas-de-ruta', {
      repartidorId: repartidor.json('id'), vehiculoId: vehiculo.json('id'), fecha, enviosIds: [], retirosIds: [retiro.json('id')],
    }, admin);
    if (ruta.status !== 201) throw new Error(`armado de ruta ${i}: ${ruta.status} ${ruta.body}`);
    const rutaId = ruta.json('hojaDeRutaId');
    const despacho = post(`/api/planificacion/hojas-de-ruta/${rutaId}/despacho`, {}, admin);
    if (despacho.status !== 200) throw new Error(`despacho ${i}: ${despacho.status} ${despacho.body}`);
    datos.reps.push({ email, rutaId, retiroId: retiro.json('id') });
  }
  return datos;
}

// ----- Escenarios -----
export function consultaPublica(datos) {
  const codigo = datos.codigoSeguimiento || 'TRK-INEXISTENTE';
  const r = http.get(`${BASE}/api/seguimiento/${codigo}`, { tags: { escenario: 'seguimiento' } });
  if (r.status === 429) limitadas.add(1);
  check(r, { 'sin error de servidor': (x) => x.status < 500 }, { escenario: 'seguimiento' });
  sleep(0.2);
}

export function altaPorApi() {
  const r = http.post(`${BASE}/api/integracion/v1/envios`, JSON.stringify({
    modalidadServicio: 'Estandar', nombreDestinatario: 'Carga', calle: 'Av. Italia', numero: '100', ciudad: 'Montevideo',
    bultos: [{ peso: 2, alto: 10, ancho: 10, profundo: 10 }], referenciaExterna: `carga-${__VU}-${__ITER}-${Date.now()}`,
  }), { headers: Object.assign({ 'X-Api-Key': CLAVE_API }, JSON_H), tags: { escenario: 'alta' } });
  if (r.status === 429) limitadas.add(1);
  check(r, { 'alta creada': (x) => x.status === 201 }, { escenario: 'alta' });
}

export function sincronizacion(datos) {
  const rep = datos.reps[__VU - 1];
  const t0 = Date.now();
  const etiqueta = { escenario: 'sync' };
  group('sincronización del repartidor', () => {
    const token = login(rep.email, 'Repartidor.2026!');
    const hoy = new Date().toISOString().slice(0, 10);
    const descarga = http.get(`${BASE}/api/mobile/v1/rutas?fecha=${hoy}`, { headers: { Authorization: `Bearer ${token}` }, tags: etiqueta });
    check(descarga, { 'descarga de la ruta': (x) => x.status === 200 }, etiqueta);
    const ruta = descarga.json('rutas').find((r) => r.hojaDeRutaId === rep.rutaId);
    const parada = ruta.paradas.find((p) => p.tipo === 'Retiro');

    const cuerpo = {
      retiroId: rep.retiroId, idempotencyKey: `carga-${rep.retiroId}`, codigosBultosEscaneados: parada.bultos.map((b) => b.identificacion),
      fotoEstadoBase64: 'Zm90bw==', firmaBase64: 'ZmlybWE=', nombreEntrega: 'Carlos', documentoEntrega: '1.111.111-1', latitud: -34.9, longitud: -56.18,
    };
    const rutaRetiro = `/api/mobile/v1/rutas/${rep.rutaId}/paradas/${parada.paradaId}/retiro`;
    const registro = post(rutaRetiro, cuerpo, token, etiqueta);
    check(registro, { 'retiro registrado': (x) => x.status === 200 }, etiqueta);

    // La app sin conexión reenvía: debe ser idempotente
    const reenvio = post(rutaRetiro, cuerpo, token, etiqueta);
    check(reenvio, { 'reenvío idempotente': (x) => x.status === 200 && x.json('idempotente') === true }, etiqueta);

    const rendicion = post(`/api/deposito/hojas-de-ruta/${rep.rutaId}/rendicion`, { codigosBultosDevueltos: [] }, datos.admin, etiqueta);
    check(rendicion, { 'rendición en depósito': (x) => x.status === 200 }, etiqueta);
  });
  duracionSync.add(Date.now() - t0);
}

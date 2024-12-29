import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = 'http://localhost:5000';

export const options = {
  scenarios: {
    endurance: {
      executor: 'constant-vus',
      vus: 40000,
      duration: '7d',
    },
  },
  thresholds: {
    'http_req_duration': ['p99<5'],
    'memory': ['max<512MB'],
  },
};

export default function () {
  const deviceId = `device_${__VU}`;
  const fileId = `file_${Math.floor(Math.random() * 20)}`;

  // Mix d'appels API simulant un usage réel
  const apis = [
    () => http.get(`${BASE_URL}/time`),
    () => http.get(`${BASE_URL}/manifest/${deviceId}`),
    () => http.get(`${BASE_URL}/download/${fileId}?offset=0&size=4096`),
    () => http.post(`${BASE_URL}/confirm/${deviceId}/${fileId}`)
  ];

  const randomApi = apis[Math.floor(Math.random() * apis.length)];
  const res = randomApi();

  check(res, {
    'status is 200': (r) => r.status === 200,
    'duration < 5ms': (r) => r.timings.duration < 5,
  });

  sleep(Math.random() * 2);
}
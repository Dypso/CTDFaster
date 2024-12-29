import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = 'http://localhost:5000';


export const options = {
  scenarios: {
    standard_load: {
      //executor: 'constant-vus',
      executor: 'constant-arrival-rate',
      rate: 6000, // Débit cible (RPS)
      //vus: 10, // Même niveau de concurrence que Bombardier
      duration: '30s', // Durée alignée
      preAllocatedVUs: 10,
      maxVUs: 100, 
      gracefulStop: '0s', // Arrêt immédiat des VUs après la durée
    },
  },
  thresholds: {
    'http_req_duration': ['p(99)<5'], // Seuil maintenu
  },
};


 /*
export const options = {
  scenarios: {
    standard_load: {
      executor: 'ramping-vus',
      startVUs: 10,
      stages: [
        { duration: '30s', target: 10 }
      ],
    }
    
   ,
    peak_load: {
      executor: 'ramping-vus',
      startTime: '45m',
      startVUs: 0,
      stages: [
        { duration: '1m', target: 500 },
        { duration: '1m', target: 800 },
        { duration: '1m', target: 1000 }
      ],
    }
  },
  thresholds: {
    'http_req_duration{scenario:time}': ['p(99)<5'],  // Correction ici
    'http_req_duration{scenario:other}': ['p(99)<5']  // Correction ici
  },
};*/

export default function () {
  const deviceId = `device_${__VU}`;
  const fileId = `file_${Math.floor(Math.random() * 20)}`;

  // Test /time endpoint
  {
    const res = http.get(`${BASE_URL}/time/${deviceId}`, {
      tags: { scenario: 'time' }  // Ajout du tag pour le threshold
    });
    check(res, {
      'time status is 200': (r) => r.status === 200,
      'time response < 5ms': (r) => r.timings.duration < 1,
    });
  }
/*
  // Test autres endpoints
  {
    const res = http.get(`${BASE_URL}/manifest/${deviceId}`, {
      tags: { scenario: 'other' }  // Ajout du tag pour le threshold
    });
    check(res, {
      'manifest status is 200': (r) => r.status === 200,
      'manifest response < 5ms': (r) => r.timings.duration < 5,
    });
  }

  // Test /download endpoint
  {
    const offset = Math.floor(Math.random() * 4096);
    const size = Math.min(4096 - offset, 1024);
    const res = http.get(
      `${BASE_URL}/download/${fileId}?offset=${offset}&size=${size}`,
      { tags: { scenario: 'other' } }
    );
    check(res, {
      'download status is 200': (r) => r.status === 200,
      'download response < 5ms': (r) => r.timings.duration < 5,
      'download size correct': (r) => r.body.length === size,
    });
  }

  // Test /confirm endpoint
  {
    const res = http.post(
      `${BASE_URL}/confirm/${deviceId}/${fileId}`,
      null,
      { tags: { scenario: 'other' } }
    );
    check(res, {
      'confirm status is 200': (r) => r.status === 200,
      'confirm response < 5ms': (r) => r.timings.duration < 5,
    });
  }
*/
  //sleep(1);
}
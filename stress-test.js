import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    stages: [
        { duration: '30s', target: 20 },  // Ramp up to 20 users
        { duration: '1m', target: 20 },   // Stay at 20 users
        { duration: '10s', target: 0 },   // Ramp down
    ],
};

export default function () {
    const res = http.get('https://localhost:7224/sk/1137/t/20251/3365');
    check(res, { 'status is 200': (r) => r.status === 200 });
    sleep(1);
}
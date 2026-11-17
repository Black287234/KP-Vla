#!/usr/bin/env bash
# Проверка API запущенного приложения: печатает код ответа и начало ответа для каждого запроса.
# Использование: bash tools/check_api.sh http://localhost:5091
B=${1:-http://localhost:5091}
fail=0
check() { # метод адрес ожидаемый_код [тело]
  local code
  if [ -n "$4" ]; then code=$(curl -s -o /tmp/r.json -w '%{http_code}' -X "$1" -H 'Content-Type: application/json' -d "$4" "$B$2")
  else code=$(curl -s -o /tmp/r.json -w '%{http_code}' -X "$1" "$B$2"); fi
  local mark="ПРОЙДЕНА"; [ "$code" != "$3" ] && { mark="ОШИБКА"; fail=1; }
  printf '%-9s %s %s %s -> %s  %s\n' "$mark" "$1" "$2" "(ожидался $3)" "$code" "$(head -c 160 /tmp/r.json)"
}
check GET / 200
check GET /api/products 200
check GET "/api/products?category=Writing" 200
check GET "/api/products?archived=true" 200
check GET /api/products/1 200
check GET /api/products/999 404
check GET /api/departments 200
check GET /api/categories 200
check POST /api/products 201 '{"article":"кт-0100","name":"Ластик","category":"Writing","unit":"шт.","minQuantity":5}'
check POST /api/products 409 '{"article":"КТ-0100","name":"Ластик","category":"Writing","unit":"шт.","minQuantity":5}'
check POST /api/products 400 '{"article":"12","name":"","category":null,"unit":"шт.","minQuantity":-1}'
check PUT /api/products/1 200 '{"article":"КТ-0001","name":"Бумага офисная А4, 80 г/м², 500 листов","category":"Paper","unit":"пачка","minQuantity":25}'
check POST /api/products/20/archive 200
check POST /api/products/20/archive 409
check POST /api/products/20/restore 200
check POST /api/departments 201 '{"name":"Методический кабинет","head":"Белова Ольга Ивановна"}'
check POST /api/departments 409 '{"name":"методический  кабинет","head":"Белова Ольга Ивановна"}'
T=$(date +%F); Y=$(date -d '-1 day' +%F); F=$(date -d '+1 day' +%F); OLD=$(date -d '-40 day' +%F)
check GET /api/stock 200
check GET /api/stock/low 200
check GET /api/reports/summary 200
check GET "/api/reports/consumption?from=$OLD&to=$T" 200
check GET "/api/reports/consumption?from=$T&to=$OLD" 400
check GET /api/products/2/movements 200
check GET "/api/movements?kind=Issue&from=$OLD" 200
check GET "/api/movements?from=$T&to=$OLD" 400
check POST /api/movements/receipt 201 "{\"productId\":2,\"quantity\":10,\"date\":\"$T\",\"supplier\":\"ООО «Офис-Комплект»\",\"documentNumber\":\"ТН-2001\"}"
check POST /api/movements/receipt 400 "{\"productId\":2,\"quantity\":10,\"date\":\"$F\",\"supplier\":\"ООО «Офис-Комплект»\"}"
check POST /api/movements/receipt 400 "{\"productId\":2,\"quantity\":0,\"date\":\"$T\",\"supplier\":\"\"}"
check POST /api/movements/issue 201 "{\"productId\":2,\"quantity\":5,\"date\":\"$T\",\"departmentId\":1,\"recipient\":\"Ковалёва И. П.\"}"
check POST /api/movements/issue 409 "{\"productId\":2,\"quantity\":100000,\"date\":\"$T\",\"departmentId\":1,\"recipient\":\"Ковалёва И. П.\"}"
check POST /api/movements/issue 404 "{\"productId\":2,\"quantity\":1,\"date\":\"$T\",\"departmentId\":999,\"recipient\":\"Ковалёва И. П.\"}"
check POST /api/movements/issue 409 "{\"productId\":19,\"quantity\":1,\"date\":\"$T\",\"departmentId\":1,\"recipient\":\"Ковалёва И. П.\"}"
check POST /api/products/2/archive 409
check GET /api/unknown 404
exit $fail

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
check POST /api/products/1/archive 200
check POST /api/products/1/archive 409
check POST /api/products/1/restore 200
check POST /api/departments 201 '{"name":"Методический кабинет","head":"Белова Ольга Ивановна"}'
check POST /api/departments 409 '{"name":"методический  кабинет","head":"Белова Ольга Ивановна"}'
check GET /api/unknown 404
exit $fail

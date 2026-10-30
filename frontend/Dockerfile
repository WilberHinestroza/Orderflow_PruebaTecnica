# ---- Etapa 1: build de producción con Node ----
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

# ---- Etapa 2: solo los archivos estáticos sobre nginx sin root (~50 MB) ----
FROM nginxinc/nginx-unprivileged:1.27-alpine
ENV ORDERS_API_URL=http://orders-api:8080 \
    INVENTORY_URL=http://inventory-worker:8080
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080

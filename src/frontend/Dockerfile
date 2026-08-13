FROM node:23-alpine AS deps
WORKDIR /app
COPY package*.json ./
RUN npm ci

FROM node:23-alpine AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
# NEXT_PUBLIC_API_URL comes from .env.production (copied with the source).
# docker-compose.local.yml overrides it to the host gateway so a locally built
# image does not point at the production API. The file is only written when the
# build arg is non-empty, so the deploy build is untouched.
ARG NEXT_PUBLIC_API_URL=""
RUN if [ -n "$NEXT_PUBLIC_API_URL" ]; then \
        echo "NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL" > .env.production.local; \
    fi
ARG NEXT_PUBLIC_GOOGLE_CLIENT_ID
ENV NEXT_PUBLIC_GOOGLE_CLIENT_ID=${NEXT_PUBLIC_GOOGLE_CLIENT_ID}
RUN npm run build

FROM node:23-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
COPY --from=builder /app/.next/standalone ./
COPY --from=builder /app/.next/static ./.next/static
COPY --from=builder /app/public ./public

COPY --from=deps /app/node_modules/pino-loki ./node_modules/pino-loki
COPY --from=deps /app/node_modules/pino-abstract-transport ./node_modules/pino-abstract-transport
COPY --from=deps /app/node_modules/pump ./node_modules/pump
COPY --from=deps /app/node_modules/split2 ./node_modules/split2
COPY --from=deps /app/node_modules/end-of-stream ./node_modules/end-of-stream
COPY --from=deps /app/node_modules/once ./node_modules/once
COPY --from=deps /app/node_modules/wrappy ./node_modules/wrappy
EXPOSE 3000
CMD ["node", "server.js"]

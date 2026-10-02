FROM nginx:stable-alpine

RUN apk add --no-cache curl brotli

COPY docker/default.conf /etc/nginx/conf.d/default.conf
COPY docker/entrypoint.sh /entrypoint.sh
COPY build/WebGL/SatTrak/ /usr/share/nginx/html/

RUN chmod +x /entrypoint.sh \
 && gzip -9 -k /usr/share/nginx/html/StreamingAssets/models/*.glb \
 && for f in /usr/share/nginx/html/Build/*.br; do brotli -d -c "$f" | gzip -9 -n > "$f.gz"; done \
 && chmod -R a+rX /usr/share/nginx/html

EXPOSE 80
ENTRYPOINT ["/entrypoint.sh"]

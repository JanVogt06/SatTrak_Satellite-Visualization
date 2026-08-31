FROM nginx:stable-alpine

RUN apk add --no-cache curl

COPY docker/default.conf /etc/nginx/conf.d/default.conf
COPY docker/entrypoint.sh /entrypoint.sh
COPY build/WebGL/SatTrak/ /usr/share/nginx/html/

RUN chmod +x /entrypoint.sh \
 && chmod -R a+rX /usr/share/nginx/html

EXPOSE 80
ENTRYPOINT ["/entrypoint.sh"]

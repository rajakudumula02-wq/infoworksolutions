FROM nginx:alpine

RUN rm -rf /usr/share/nginx/html/*

COPY website/ /usr/share/nginx/html/

# Move nginx config to proper location
RUN mv /usr/share/nginx/html/nginx.conf /etc/nginx/conf.d/default.conf

# Rename homepage to index.html
RUN mv /usr/share/nginx/html/infoworks-homepage.html /usr/share/nginx/html/index.html || true

# Also serve from /lander path
RUN mkdir -p /usr/share/nginx/html/lander && \
    cp /usr/share/nginx/html/*.html /usr/share/nginx/html/lander/ && \
    cp /usr/share/nginx/html/*.png /usr/share/nginx/html/lander/ 2>/dev/null || true

RUN ls -la /usr/share/nginx/html/

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]

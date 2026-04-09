FROM nginx:alpine

RUN rm -rf /usr/share/nginx/html/*

# Copy from top-level website folder
COPY website/ /usr/share/nginx/html/

# Rename homepage to index.html
RUN mv /usr/share/nginx/html/infoworks-homepage.html /usr/share/nginx/html/index.html || true

# Also serve from /lander path
RUN mkdir -p /usr/share/nginx/html/lander && \
    cp /usr/share/nginx/html/*.html /usr/share/nginx/html/lander/ && \
    cp /usr/share/nginx/html/*.png /usr/share/nginx/html/lander/ 2>/dev/null || true

# Custom nginx config
RUN printf 'server {\n\
    listen 80;\n\
    server_name _;\n\
    root /usr/share/nginx/html;\n\
    index index.html;\n\
    location / {\n\
        try_files $uri $uri/ /index.html;\n\
    }\n\
    location /lander {\n\
        alias /usr/share/nginx/html/lander;\n\
        index index.html;\n\
        try_files $uri $uri/ /lander/index.html;\n\
    }\n\
}\n' > /etc/nginx/conf.d/default.conf

RUN ls -la /usr/share/nginx/html/

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]

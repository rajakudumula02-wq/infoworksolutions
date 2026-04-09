FROM nginx:alpine
COPY *.html /usr/share/nginx/html/
COPY *.png /usr/share/nginx/html/
COPY infoworks-homepage.html /usr/share/nginx/html/index.html
RUN mkdir -p /usr/share/nginx/html/lander && \
    cp /usr/share/nginx/html/*.html /usr/share/nginx/html/lander/ && \
    cp /usr/share/nginx/html/*.png /usr/share/nginx/html/lander/ 2>/dev/null || true
RUN ls -la /usr/share/nginx/html/
EXPOSE 80

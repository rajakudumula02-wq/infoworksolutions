FROM nginx:alpine
COPY . /usr/share/nginx/html/
RUN if [ -f /usr/share/nginx/html/infoworks-homepage.html ]; then cp /usr/share/nginx/html/infoworks-homepage.html /usr/share/nginx/html/index.html; fi
RUN rm -f /usr/share/nginx/html/Dockerfile
RUN mkdir -p /usr/share/nginx/html/lander && \
    cp /usr/share/nginx/html/*.html /usr/share/nginx/html/lander/ && \
    cp /usr/share/nginx/html/*.png /usr/share/nginx/html/lander/ 2>/dev/null || true
RUN ls -la /usr/share/nginx/html/
EXPOSE 80

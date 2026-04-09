FROM nginx:alpine
COPY website/ /usr/share/nginx/html/
RUN if [ -f /usr/share/nginx/html/infoworks-homepage.html ]; then cp /usr/share/nginx/html/infoworks-homepage.html /usr/share/nginx/html/index.html; fi
RUN ls -la /usr/share/nginx/html/
EXPOSE 80

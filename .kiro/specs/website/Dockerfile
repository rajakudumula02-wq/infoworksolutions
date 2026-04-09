FROM nginx:alpine

# Copy all website files
COPY *.html /usr/share/nginx/html/
COPY *.png /usr/share/nginx/html/

# Create index.html from homepage
COPY infoworks-homepage.html /usr/share/nginx/html/index.html

# Debug: show what's there
RUN ls -la /usr/share/nginx/html/

EXPOSE 80

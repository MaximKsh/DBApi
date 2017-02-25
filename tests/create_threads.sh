#curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{  
 #  "author": "anton", 
 #  "forum": "pirate-stories", 
 #  "message": "message1",
 #  "slug": "jones-cache", 
 #  "title": "Davy Jones cache", 
 #}' 'localhost:5000/api/forum/maxim-forum/create'



 curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{  
   "author": "maxim", 
   "message": "message1",
   "title": "Davy Jones cache", 
 }' 'localhost:5000/api/forum/anton-forum/create'

 # curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{  
 #  "author": "gena", 
 #  "message": "message1",
 #  "title": "Davy Jones cache", 
 #}' 'localhost:5000/api/forum/maxim-forum/create'
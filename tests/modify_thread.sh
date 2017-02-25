curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{  
   "message": "New Message With New Title", 
   "title": "New Updated Thread" 
 }' 'localhost:5000/api/thread/30/details'

 curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "message": "New Message With Without Title"
 }' 'localhost:5000/api/thread/32/details'

  curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "title" : "just title"
 }' 'localhost:5000/api/thread/37/details'


   curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "title" : "not fount"
 }' 'localhost:5000/api/thread/100500/details'
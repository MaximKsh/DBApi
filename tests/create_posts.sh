curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '[ 
   { 
     "author": "maxim", 
     "created": "2017-02-24T09:53:44.938Z", 
     "forum": "string", 
     "id": 122, 
     "isEdited": true, 
     "message": "We should be afraid of the Kraken.", 
     "parent": 1222, 
     "thread": 0 
   }, 
 { 
     "author": "maxim", 
     "created": "2017-02-24T09:53:44.938Z", 
     "forum": "string", 
     "id": 0, 
     "isEdited": true, 
     "message": "We should be afraid of the Kraken.", 
     "parent": 0, 
     "thread": 0 
   } 
 ]' 'localhost:5000/api/thread/jones-cache/create'
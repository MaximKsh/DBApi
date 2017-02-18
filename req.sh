curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "posts": 200000, 
   "slug": "pirate-stories",  
   "threads": 200, 
   "user": "16"  
 }' 'localhost:5000/api/forum/create'
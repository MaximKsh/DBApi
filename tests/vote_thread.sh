curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "nickname": "anton",
   "voice": 1 
 }' 'localhost:5000/api/thread/33/vote'
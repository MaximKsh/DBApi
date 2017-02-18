curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "about": "I am maxim", 
   "email": "ksh.max@gmail.com", 
   "fullname": "Maxim Kashirin", 
 }' 'localhost:5000/api/user/maxim/create'


curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "about": "I am petr", 
   "email": "petr@gmail.com", 
 }' 'localhost:5000/api/user/petr/create'


curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "about": "I am ivan", 
   "email": "ivan@gmail.com", 
 }' 'localhost:5000/api/user/ivan/create'

curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "about": "I am gena", 
 }' 'localhost:5000/api/user/gena/create'

curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "email": "andrew@gmail.com", 
 }' 'localhost:5000/api/user/andrew/create'


curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "email": "anton@gmail.com", 
 }' 'localhost:5000/api/user/anton/create'

curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{  
 }' 'localhost:5000/api/user/oleg/create'

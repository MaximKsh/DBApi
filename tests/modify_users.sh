curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "about": "I am ivan and its changed", 
   "email": "changed (ivan)",  
   "fullname": "ivan changed" 
 }' 'localhost:5000/api/user/ivan/profile'


curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "about": "I am oleg and its changed", 
   "email": "oleg@gmail.com",  
 }' 'localhost:5000/api/user/oleg/profile'

curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "about": "I am gena and its changed",
 }' 'localhost:5000/api/user/gena/profile'

curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "about": "I am gena and its changed",
 }' 'localhost:5000/api/user/gena/profile'


curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "about": "I am anton and its changed", 
   "email": "ksh.max@gmail.com",  
 }' 'localhost:5000/api/user/anton/profile'

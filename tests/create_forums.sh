curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "slug": "maxim-forum", 
   "title": "This is maxim forum", 
   "user": "maxim"
 }' 'localhost:5000/api/forum/create'


 curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "slug": "anton-forum", 
   "user": "anton"
 }' 'localhost:5000/api/forum/create'

 curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "slug": "maxim-forum2", 
   "title": "This is second maxim forum", 
   "user": "maxim"
 }' 'localhost:5000/api/forum/create'

  curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "title": "This is ivan forum", 
   "user": "ivan"
 }' 'localhost:5000/api/forum/create'

   curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "user": "gena"
 }' 'localhost:5000/api/forum/create'

    curl -X POST --header 'Content-Type: application/json' --header 'Accept: application/json' -d '{ 
   "user": "hzkto"
 }' 'localhost:5000/api/forum/create'
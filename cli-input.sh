cat | sed -u "s/'/'\"'\"'/g" | stdbuf -o0 tr '\n' '\0' | xargs -0 -I % sh -c "printf '%' | nc -N localhost 4000"

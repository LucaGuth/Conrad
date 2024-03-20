ncat -lk 4001 -c 'var="[conrad] $(tee)"; echo $var 1>&2' --recv-only
